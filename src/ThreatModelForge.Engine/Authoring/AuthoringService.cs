namespace ThreatModelForge.Engine
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ThreatModelForge.Formats;
    using ThreatModelForge.Model;

    /// <summary>
    /// The stateless authoring facade over <see cref="AuthoringOperations"/> and
    /// <see cref="ManifestSupport"/>: it takes a canonical tmforge-json model in and returns the edited
    /// model out, so the HTTP API, the WASM shim, and an MCP server can drive imperative authoring
    /// (<c>add</c>, <c>connect</c>, <c>set</c>, <c>rename</c>, <c>remove</c>) and declarative manifests
    /// (<c>apply</c>, <c>export</c>) through the same implementation the CLI uses. Every call is
    /// self-contained (model in, model out) — there is no server-side session state.
    /// </summary>
    public static class AuthoringService
    {
        /// <summary>
        /// Adds a process, data store, external interactor, or trust boundary to the model.
        /// </summary>
        /// <param name="model">The canonical model to edit, or <see langword="null"/> to start from an empty model.</param>
        /// <param name="request">The add inputs.</param>
        /// <returns>The edited model and the new element id, or a blocking error.</returns>
        public static AuthoringResultDto Add(TmForgeModelDto? model, AddRequest request)
        {
            ThreatModel tm = ModelDtoMapper.ToModel(model ?? new TmForgeModelDto());
            if (!AuthoringOperations.Add(tm, request, out Guid id, out IReadOnlyList<string> warnings, out string? error))
            {
                return new AuthoringResultDto { Success = false, Error = error, Warnings = warnings };
            }

            return new AuthoringResultDto
            {
                Success = true,
                Warnings = warnings,
                Model = ModelDtoMapper.ToDto(tm),
                Id = id.ToString(),
            };
        }

        /// <summary>
        /// Adds a data-flow connector between two existing elements on the same page.
        /// </summary>
        /// <param name="model">The canonical model to edit, or <see langword="null"/> to start from an empty model.</param>
        /// <param name="request">The connect inputs.</param>
        /// <returns>The edited model, the connector id, and the resolved endpoints, or a blocking error.</returns>
        public static AuthoringResultDto Connect(TmForgeModelDto? model, ConnectRequest request)
        {
            ThreatModel tm = ModelDtoMapper.ToModel(model ?? new TmForgeModelDto());
            if (!AuthoringOperations.Connect(tm, request, out Guid id, out Guid source, out Guid target, out IReadOnlyList<string> warnings, out string? error))
            {
                return new AuthoringResultDto { Success = false, Error = error, Warnings = warnings };
            }

            return new AuthoringResultDto
            {
                Success = true,
                Warnings = warnings,
                Model = ModelDtoMapper.ToDto(tm),
                Id = id.ToString(),
                Source = source.ToString(),
                Target = target.ToString(),
            };
        }

        /// <summary>
        /// Sets the name and/or custom properties of an existing element or flow.
        /// </summary>
        /// <param name="model">The canonical model to edit, or <see langword="null"/> to start from an empty model.</param>
        /// <param name="request">The set inputs.</param>
        /// <returns>The edited model and the resolved id, or a blocking error.</returns>
        public static AuthoringResultDto Set(TmForgeModelDto? model, SetRequest request)
        {
            ThreatModel tm = ModelDtoMapper.ToModel(model ?? new TmForgeModelDto());
            if (!AuthoringOperations.Set(tm, request, out Guid id, out IReadOnlyList<string> warnings, out string? error))
            {
                return new AuthoringResultDto { Success = false, Error = error, Warnings = warnings };
            }

            return new AuthoringResultDto
            {
                Success = true,
                Warnings = warnings,
                Model = ModelDtoMapper.ToDto(tm),
                Id = id.ToString(),
            };
        }

        /// <summary>
        /// Sets the display name of an existing element.
        /// </summary>
        /// <param name="model">The canonical model to edit, or <see langword="null"/> to start from an empty model.</param>
        /// <param name="request">The rename inputs.</param>
        /// <returns>The edited model and the resolved id, or a blocking error.</returns>
        public static AuthoringResultDto Rename(TmForgeModelDto? model, RenameRequest request)
        {
            ThreatModel tm = ModelDtoMapper.ToModel(model ?? new TmForgeModelDto());
            if (!AuthoringOperations.Rename(tm, request, out Guid id, out string? error))
            {
                return new AuthoringResultDto { Success = false, Error = error };
            }

            return new AuthoringResultDto
            {
                Success = true,
                Model = ModelDtoMapper.ToDto(tm),
                Id = id.ToString(),
            };
        }

        /// <summary>
        /// Removes an element (and any data flows attached to it).
        /// </summary>
        /// <param name="model">The canonical model to edit, or <see langword="null"/> to start from an empty model.</param>
        /// <param name="request">The remove inputs.</param>
        /// <returns>The edited model and the removed identifiers, or a blocking error.</returns>
        public static AuthoringResultDto Remove(TmForgeModelDto? model, RemoveRequest request)
        {
            ThreatModel tm = ModelDtoMapper.ToModel(model ?? new TmForgeModelDto());
            if (!AuthoringOperations.Remove(tm, request, out IReadOnlyList<Guid> removed, out string? error))
            {
                return new AuthoringResultDto { Success = false, Error = error };
            }

            return new AuthoringResultDto
            {
                Success = true,
                Model = ModelDtoMapper.ToDto(tm),
                Removed = removed.Select(guid => guid.ToString()).ToList(),
            };
        }

        /// <summary>
        /// Materializes a declarative manifest into a model (all-or-nothing).
        /// </summary>
        /// <param name="manifest">The manifest to build.</param>
        /// <param name="force">Whether to store unknown/invalid property values instead of rejecting them.</param>
        /// <returns>The built model and the counts of what was created, or a blocking error.</returns>
        public static ApplyResultDto Apply(Manifest manifest, bool force)
        {
            if (!ManifestSupport.Build(manifest, force, out ThreatModel tm, out ManifestSummary summary, out string? error))
            {
                return new ApplyResultDto { Success = false, Error = error };
            }

            return new ApplyResultDto
            {
                Success = true,
                Model = ModelDtoMapper.ToDto(tm),
                Boundaries = summary.Boundaries,
                Elements = summary.Elements,
                Flows = summary.Flows,
            };
        }

        /// <summary>
        /// Extracts a declarative manifest from a model, so an agent can round-trip a model authored any
        /// way back into a review-friendly source.
        /// </summary>
        /// <param name="model">The canonical model to capture.</param>
        /// <returns>The extracted manifest.</returns>
        public static Manifest ExportManifest(TmForgeModelDto? model)
        {
            ThreatModel tm = ModelDtoMapper.ToModel(model ?? new TmForgeModelDto());
            return ManifestSupport.Extract(tm);
        }

        /// <summary>
        /// Adds a manually-authored STRIDE threat to the model's author overlay — a threat the rules do
        /// not detect. It is keyed <c>manual:{guid}</c> and carries the author's category, title, state,
        /// priority, description, mitigation, and scope. The overlay round-trips into the exported
        /// <c>.tm7</c> register.
        /// </summary>
        /// <param name="model">The canonical model to edit, or <see langword="null"/> to start from an empty model.</param>
        /// <param name="request">The threat inputs.</param>
        /// <returns>The edited model and the new threat id, or a blocking error.</returns>
        public static AuthoringResultDto AddThreat(TmForgeModelDto? model, AddThreatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return new AuthoringResultDto { Success = false, Error = "A threat title is required." };
            }

            if (string.IsNullOrWhiteSpace(request.Category))
            {
                return new AuthoringResultDto { Success = false, Error = "A STRIDE category is required." };
            }

            if (!TryCanonicalPriority(request.Priority, out string? priority))
            {
                return new AuthoringResultDto { Success = false, Error = "Priority must be High, Medium, or Low." };
            }

            TmForgeModelDto source = model ?? new TmForgeModelDto();
            string id = "manual:" + Guid.NewGuid().ToString("N");
            ThreatStateDto entry = new ThreatStateDto
            {
                Id = id,
                Manual = true,
                State = ThreatStateWire.Canonical(request.State),
                Category = request.Category,
                Title = request.Title,
                Description = NullIfBlank(request.Description),
                Mitigation = NullIfBlank(request.Mitigation),
                Priority = priority,
                ElementIds = request.ElementIds != null && request.ElementIds.Count > 0 ? request.ElementIds.ToList() : null,
            };
            List<ThreatStateDto> overlay = new List<ThreatStateDto>(source.Threats ?? Array.Empty<ThreatStateDto>()) { entry };
            return new AuthoringResultDto { Success = true, Model = WithThreats(source, overlay), Id = id };
        }

        /// <summary>
        /// Edits a threat's author-owned fields (state, priority, mitigation, description, justification)
        /// on the model's overlay. Records an overlay entry keyed by the threat's register id, so the edit
        /// is applied when the threat is next projected and round-trips into the <c>.tm7</c> register.
        /// </summary>
        /// <param name="model">The canonical model to edit, or <see langword="null"/> to start from an empty model.</param>
        /// <param name="request">The edit inputs.</param>
        /// <returns>The edited model, or a blocking error.</returns>
        public static AuthoringResultDto EditThreat(TmForgeModelDto? model, EditThreatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Id))
            {
                return new AuthoringResultDto { Success = false, Error = "A threat id is required." };
            }

            if (!TryCanonicalPriority(request.Priority, out string? priority))
            {
                return new AuthoringResultDto { Success = false, Error = "Priority must be High, Medium, or Low." };
            }

            TmForgeModelDto source = model ?? new TmForgeModelDto();
            List<ThreatStateDto> overlay = new List<ThreatStateDto>(source.Threats ?? Array.Empty<ThreatStateDto>());
            int index = overlay.FindIndex(entry => string.Equals(entry.Id, request.Id, StringComparison.OrdinalIgnoreCase));
            ThreatStateDto existing = index >= 0 ? overlay[index] : new ThreatStateDto { Id = request.Id };
            bool manual = existing.Manual == true || ThreatStateWire.IsManualKey(request.Id);
            ThreatStateDto updated = new ThreatStateDto
            {
                Id = request.Id,
                Manual = manual ? true : (bool?)null,
                State = request.State != null ? ThreatStateWire.Canonical(request.State) : existing.State,
                Justification = request.Justification ?? existing.Justification,
                Priority = priority ?? existing.Priority,
                Description = request.Description ?? existing.Description,
                Mitigation = request.Mitigation ?? existing.Mitigation,
                Category = existing.Category,
                Title = existing.Title,
                ElementIds = existing.ElementIds,
            };
            if (index >= 0)
            {
                overlay[index] = updated;
            }
            else
            {
                overlay.Add(updated);
            }

            return new AuthoringResultDto { Success = true, Model = WithThreats(source, overlay), Id = request.Id };
        }

        /// <summary>
        /// Removes a threat's author-overlay entry: a manually-authored threat is deleted, and an edited
        /// rule threat is reset to its generated defaults. A rule threat without an edit has no overlay
        /// entry to remove.
        /// </summary>
        /// <param name="model">The canonical model to edit, or <see langword="null"/> to start from an empty model.</param>
        /// <param name="request">The remove inputs.</param>
        /// <returns>The edited model and the removed id, or a blocking error.</returns>
        public static AuthoringResultDto RemoveThreat(TmForgeModelDto? model, RemoveThreatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Id))
            {
                return new AuthoringResultDto { Success = false, Error = "A threat id is required." };
            }

            TmForgeModelDto source = model ?? new TmForgeModelDto();
            List<ThreatStateDto> overlay = new List<ThreatStateDto>(source.Threats ?? Array.Empty<ThreatStateDto>());
            int removed = overlay.RemoveAll(entry => string.Equals(entry.Id, request.Id, StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
            {
                return new AuthoringResultDto { Success = false, Error = "No author-owned overlay entry for threat: " + request.Id + " (a rule threat without edits regenerates from the rules)." };
            }

            return new AuthoringResultDto { Success = true, Model = WithThreats(source, overlay), Removed = new[] { request.Id } };
        }

        private static bool TryCanonicalPriority(string? value, out string? priority)
        {
            priority = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            string[] priorities = { "High", "Medium", "Low" };
            priority = priorities.FirstOrDefault(candidate =>
                string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));
            return priority != null;
        }

        private static TmForgeModelDto WithThreats(TmForgeModelDto source, List<ThreatStateDto> overlay)
        {
            return new TmForgeModelDto
            {
                Schema = source.Schema,
                Version = source.Version,
                Elements = source.Elements,
                Flows = source.Flows,
                Diagrams = source.Diagrams,
                Analysis = source.Analysis,
                Threats = overlay.Count > 0 ? overlay : null,
            };
        }

        private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
