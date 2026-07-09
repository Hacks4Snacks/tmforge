namespace ThreatModelForge.Engine
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
    }
}
