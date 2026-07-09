namespace ThreatModelForge.Engine
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using ThreatModelForge.Analysis;
    using ThreatModelForge.Analysis.Rules;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Formats;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Reporting;

    /// <summary>
    /// Bridges the canonical tmforge-json contract to the real .NET engine: it builds a
    /// <see cref="ThreatModel"/> with the UI-agnostic <see cref="DiagramEditor"/>, runs the real
    /// analysis rule set, and serializes to the lossless <c>.tm7</c> format via the format registry.
    /// </summary>
    public static class EngineService
    {
        private static readonly JsonSerializerOptions CanonicalJsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>
        /// Lists the registered file-format providers and their capabilities.
        /// </summary>
        /// <returns>The available formats.</returns>
        public static IReadOnlyList<FormatDto> GetFormats()
        {
            ThreatModelFormatRegistry registry = ThreatModelFormatRegistry.CreateDefault();
            List<FormatDto> result = new List<FormatDto>();
            foreach (IThreatModelFormat format in registry.Formats)
            {
                result.Add(MapFormat(format));
            }

            return result;
        }

        /// <summary>
        /// Lists the built-in authoring stencils offered to the palette.
        /// </summary>
        /// <returns>The available stencils.</returns>
        public static IReadOnlyList<StencilDto> GetStencils() => StencilCatalog.All;

        /// <summary>
        /// Lists the stencil packs offered to the palette, for show/hide toggles.
        /// </summary>
        /// <returns>The available stencil packs.</returns>
        public static IReadOnlyList<PackDto> GetStencilPacks() => StencilCatalog.Packs;

        /// <summary>
        /// Lists the typed property schema (the known element properties, each with its value kind,
        /// allowed values, and default) so authoring surfaces can render typed controls and emit
        /// canonical values that the analysis rules read with confidence.
        /// </summary>
        /// <returns>The property descriptors across all DFD primitives.</returns>
        public static IReadOnlyList<PropertyDescriptor> GetPropertySchema() => PropertySchemaCatalog.All;

        /// <summary>
        /// Lists the analysis rules offered by the engine, with their pack, severity, and help link.
        /// </summary>
        /// <returns>The available rules, ordered by id.</returns>
        public static IReadOnlyList<RuleDto> GetRules()
        {
            List<RuleDto> result = new List<RuleDto>();
            using (RuleSet ruleSet = LoadRuleSet())
            {
                foreach (Rule rule in ruleSet.Rules)
                {
                    result.Add(new RuleDto
                    {
                        Id = rule.ID,
                        Pack = rule.Pack,
                        Severity = MapSeverity(rule.Severity),
                        Description = rule.FullDescription,
                        HelpText = rule.HelpText,
                        HelpUri = rule.HelpUri?.ToString(),
                    });
                }
            }

            result.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            return result;
        }

        /// <summary>
        /// Lists the rule packs offered by the engine, for per-model validation toggles.
        /// </summary>
        /// <returns>The available rule packs, in presentation order.</returns>
        public static IReadOnlyList<RulePackDto> GetRulePacks()
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            using (RuleSet ruleSet = LoadRuleSet())
            {
                foreach (Rule rule in ruleSet.Rules)
                {
                    counts[rule.Pack] = counts.TryGetValue(rule.Pack, out int existing) ? existing + 1 : 1;
                }
            }

            List<RulePackDto> result = new List<RulePackDto>();
            foreach (KeyValuePair<string, string> pack in RulePackCatalog.Ordered)
            {
                if (counts.TryGetValue(pack.Key, out int known))
                {
                    result.Add(new RulePackDto { Id = pack.Key, Name = pack.Value, Count = known });
                    counts.Remove(pack.Key);
                }
            }

            List<string> remaining = new List<string>(counts.Keys);
            remaining.Sort(StringComparer.Ordinal);
            foreach (string packId in remaining)
            {
                result.Add(new RulePackDto { Id = packId, Name = RulePackCatalog.DisplayName(packId), Count = counts[packId] });
            }

            return result;
        }

        /// <summary>
        /// Runs the real analysis rule set (ThreatModelForge.Analysis.Rules) over the model.
        /// </summary>
        /// <param name="dto">The canonical model.</param>
        /// <returns>The findings produced by the engine.</returns>
        public static IReadOnlyList<FindingDto> Analyze(TmForgeModelDto dto)
        {
            List<FindingDto> findings = new List<FindingDto>();
            try
            {
                ThreatModel model = BuildModel(dto, out Dictionary<string, List<string>> nameToIds);
                using (RuleSet ruleSet = LoadRuleSet())
                {
                    if (dto.Analysis != null)
                    {
                        ruleSet.Disable(dto.Analysis.DisabledPacks, dto.Analysis.DisabledRuleIds);
                    }

                    CollectingMessageWriter writer = new CollectingMessageWriter();
                    RuleEvaluationContext context = new RuleEvaluationContext(model, writer);
                    ruleSet.Evaluate(context);
                    ModelReport report = context.GenerateReport(ruleSet);

                    int sequence = 0;
                    foreach (RuleReport ruleReport in report.RuleReports)
                    {
                        foreach (RuleReportMessage message in ruleReport.Messages)
                        {
                            findings.Add(new FindingDto
                            {
                                Id = $"{ruleReport.ID}:{sequence++}",
                                Severity = MapSeverity(ruleReport.Severity),
                                RuleId = ruleReport.ID,
                                Message = message.Text ?? string.Empty,
                                ElementIds = ResolveIds(message.Entity, nameToIds),
                            });
                        }
                    }
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                findings.Add(new FindingDto
                {
                    Id = "engine-error",
                    Severity = "warning",
                    Message = $"Engine analysis failed: {ex.Message}",
                });
            }

            return findings;
        }

        /// <summary>
        /// Projects the model's analysis findings into STRIDE threats. Detection is entirely the
        /// rule set's — this runs the same rules <see cref="Analyze"/> runs and frames the findings
        /// from threat-bearing rules as persistable threats. CLI, <c>/v1</c>, and WASM call the same
        /// projector, so results are identical by construction.
        /// </summary>
        /// <param name="dto">The canonical model.</param>
        /// <returns>The generated threats.</returns>
        public static IReadOnlyList<ThreatDto> GenerateThreats(TmForgeModelDto dto)
        {
            List<ThreatDto> result = new List<ThreatDto>();
            try
            {
                ThreatModel model = BuildModel(dto, out Dictionary<string, List<string>> nameToIds);
                using (RuleSet ruleSet = LoadRuleSet())
                {
                    if (dto.Analysis != null)
                    {
                        ruleSet.Disable(dto.Analysis.DisabledPacks, dto.Analysis.DisabledRuleIds);
                    }

                    GenerationResult generation = ThreatGenerator.Generate(model, ruleSet);
                    Dictionary<string, ThreatStateDto> overlay = BuildTriage(dto.Threats);
                    HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (GeneratedThreat threat in generation.Threats)
                    {
                        overlay.TryGetValue(threat.Id, out ThreatStateDto? edit);
                        seen.Add(threat.Id);
                        result.Add(new ThreatDto
                        {
                            Id = threat.Id,
                            RuleId = threat.RuleId,
                            Category = threat.Category.ToString(),
                            Title = string.IsNullOrEmpty(edit?.Title) ? threat.Title : edit!.Title!,
                            Mitigation = string.IsNullOrEmpty(edit?.Mitigation) ? threat.Mitigation : edit!.Mitigation,
                            Description = edit?.Description,
                            Severity = threat.Severity,
                            Priority = string.IsNullOrEmpty(edit?.Priority) ? threat.Priority : edit!.Priority,
                            References = threat.References.Select(r => r.Id).ToList(),
                            ElementIds = BuildElementIds(threat),
                            Interaction = threat.InteractionString,
                            State = NormalizeState(edit?.State),
                            Justification = edit?.Justification,
                            Manual = false,
                        });
                    }

                    AppendManualThreats(result, dto.Threats, seen, nameToIds);
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                result.Add(new ThreatDto
                {
                    Id = "engine-error",
                    Severity = "error",
                    Title = $"Threat generation failed: {ex.Message}",
                });
            }

            return result;
        }

        /// <summary>
        /// Serializes the supplied model to lossless <c>.tm7</c> bytes via the real engine.
        /// </summary>
        /// <param name="dto">The canonical model.</param>
        /// <returns>The <c>.tm7</c> document bytes.</returns>
        public static byte[] ExportTm7(TmForgeModelDto dto)
        {
            ThreatModel model = BuildModelForExport(dto);
            Tm7ExportPreparer.Prepare(model);

            using (MemoryStream stream = new MemoryStream())
            {
                model.Save(stream);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Reads a document in any registered format and projects it onto the canonical
        /// tmforge-json model the canvas edits.
        /// </summary>
        /// <param name="content">The raw document bytes.</param>
        /// <param name="formatId">An optional explicit format id; when omitted the engine sniffs the content.</param>
        /// <returns>The canonical model.</returns>
        public static TmForgeModelDto ReadModel(byte[] content, string? formatId)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            ThreatModelFormatRegistry registry = ThreatModelFormatRegistry.CreateDefault();
            ThreatModel model;
            using (MemoryStream input = new MemoryStream(content))
            {
                model = registry.Load(input, string.IsNullOrEmpty(formatId) ? null : formatId);
            }

            return ToDto(model);
        }

        /// <summary>
        /// Serializes the supplied model to the requested registered format's bytes.
        /// </summary>
        /// <param name="dto">The canonical model.</param>
        /// <param name="formatId">The target format id (for example, <c>tm7</c>, <c>drawio</c>, <c>vsdx</c>).</param>
        /// <returns>The serialized document bytes.</returns>
        public static byte[] Convert(TmForgeModelDto dto, string formatId)
        {
            if (string.IsNullOrEmpty(formatId))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(formatId));
            }

            // .tm7 is the lossless, register-bearing format, so materialize the full threat register
            // (with acceptance) and prepare it for the Microsoft Threat Modeling Tool — embed the
            // knowledge base and type schema-backed properties — so every path that writes a .tm7 (this
            // facade, the CLI, and the dedicated export) produces the same tool-openable file. The other
            // formats drop the register, so keep the cheaper build.
            ThreatModel model;
            if (string.Equals(formatId, Tm7Format.FormatId, StringComparison.OrdinalIgnoreCase))
            {
                model = BuildModelForExport(dto);
                Tm7ExportPreparer.Prepare(model);
            }
            else
            {
                model = BuildModel(dto, out _);
            }

            ThreatModelFormatRegistry registry = ThreatModelFormatRegistry.CreateDefault();
            IThreatModelFormat format = registry.FindById(formatId)
                ?? throw new NotSupportedException($"No threat model format with id '{formatId}'.");
            if (!format.Capabilities.CanWrite)
            {
                throw new NotSupportedException($"Format '{formatId}' does not support writing.");
            }

            using (MemoryStream stream = new MemoryStream())
            {
                format.Write(model, stream);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Detects the format of a document by content sniffing.
        /// </summary>
        /// <param name="content">The raw document bytes.</param>
        /// <returns>The detected format, or <see langword="null"/> if none matches.</returns>
        public static FormatDto? Detect(byte[] content)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            ThreatModelFormatRegistry registry = ThreatModelFormatRegistry.CreateDefault();
            using (MemoryStream input = new MemoryStream(content))
            {
                IThreatModelFormat? format = registry.Sniff(input);
                return format == null ? null : MapFormat(format);
            }
        }

        /// <summary>
        /// Renders a report for the supplied model.
        /// </summary>
        /// <param name="dto">The canonical model.</param>
        /// <param name="format">The report format: <c>html</c> (default) or <c>svg</c>.</param>
        /// <returns>The report bytes (UTF-8).</returns>
        public static byte[] Report(TmForgeModelDto dto, string format)
        {
            ThreatModel model = BuildModel(dto, out _);
            if (string.Equals(format, "svg", StringComparison.OrdinalIgnoreCase))
            {
                return Encoding.UTF8.GetBytes(new DiagramSvgRenderer().RenderModel(model).ToString());
            }

            string html = new HtmlReportWriter().Write(model);
            return Encoding.UTF8.GetBytes(html);
        }

        /// <summary>
        /// Merges two edited models, keyed by element identity. When <paramref name="baseModel"/> is
        /// supplied it is a three-way merge against that common ancestor, so non-overlapping edits
        /// combine automatically; when it is <c>null</c> (the ancestor is unavailable) it falls back
        /// to a two-way merge where any divergence on a shared element is reported as a conflict.
        /// Genuine conflicts are resolved in favor of <paramref name="ours"/> and reported so the
        /// caller can present them for resolution.
        /// </summary>
        /// <param name="baseModel">The common ancestor model, or <c>null</c> for a two-way merge.</param>
        /// <param name="ours">The local model.</param>
        /// <param name="theirs">The incoming model.</param>
        /// <returns>The merged model and any conflicts (all resolved to <c>ours</c>).</returns>
        public static MergeResultDto Merge(TmForgeModelDto? baseModel, TmForgeModelDto ours, TmForgeModelDto theirs)
        {
            ThreatModel oursTm = BuildModel(ours ?? new TmForgeModelDto(), out _);
            ThreatModel theirsTm = BuildModel(theirs ?? new TmForgeModelDto(), out _);

            MergeResult result = baseModel == null
                ? ModelMerge.Merge(oursTm, theirsTm)
                : ModelMerge.Merge(BuildModel(baseModel, out _), oursTm, theirsTm);

            List<MergeConflictDto> conflicts = new List<MergeConflictDto>();
            foreach (MergeConflict conflict in result.Conflicts)
            {
                conflicts.Add(new MergeConflictDto
                {
                    ElementId = conflict.ElementId.ToString(),
                    ElementKind = conflict.ElementKind,
                    Name = conflict.Name,
                    DiagramName = conflict.DiagramName,
                    Kind = conflict.Kind.ToString(),
                    Property = conflict.Property,
                    Base = conflict.Base,
                    Ours = conflict.Ours,
                    Theirs = conflict.Theirs,
                });
            }

            return new MergeResultDto { Merged = ToDto(result.Merged), Conflicts = conflicts };
        }

        private static IReadOnlyList<string> BuildElementIds(GeneratedThreat threat)
        {
            List<string> ids = new List<string> { threat.SourceGuid.ToString() };
            if (threat.IsFlowScoped)
            {
                ids.Add(threat.TargetGuid.ToString());
                ids.Add(threat.FlowGuid.ToString());
            }

            return ids;
        }

        private static Dictionary<string, ThreatStateDto> BuildTriage(IReadOnlyList<ThreatStateDto>? states)
        {
            Dictionary<string, ThreatStateDto> map = new Dictionary<string, ThreatStateDto>(StringComparer.OrdinalIgnoreCase);
            if (states != null)
            {
                foreach (ThreatStateDto state in states)
                {
                    if (!string.IsNullOrEmpty(state.Id))
                    {
                        map[state.Id] = state;
                    }
                }
            }

            return map;
        }

        private static string NormalizeState(string? state) => ThreatStateWire.Canonical(state);

        private static void AppendManualThreats(
            List<ThreatDto> result,
            IReadOnlyList<ThreatStateDto>? overlay,
            HashSet<string> seen,
            Dictionary<string, List<string>> nameToIds)
        {
            if (overlay == null)
            {
                return;
            }

            Dictionary<string, string> idToName = InvertNames(nameToIds);
            foreach (ThreatStateDto entry in overlay)
            {
                if (entry.Manual != true || string.IsNullOrEmpty(entry.Id) || !seen.Add(entry.Id))
                {
                    continue;
                }

                IReadOnlyList<string> ids = entry.ElementIds ?? Array.Empty<string>();
                result.Add(new ThreatDto
                {
                    Id = entry.Id,
                    RuleId = string.Empty,
                    Category = entry.Category ?? string.Empty,
                    Title = entry.Title ?? string.Empty,
                    Mitigation = entry.Mitigation,
                    Description = entry.Description,
                    Severity = "warning",
                    Priority = string.IsNullOrEmpty(entry.Priority) ? "Medium" : entry.Priority!,
                    References = Array.Empty<string>(),
                    ElementIds = ids,
                    Interaction = DescribeScope(ids, idToName),
                    State = NormalizeState(entry.State),
                    Justification = entry.Justification,
                    Manual = true,
                });
            }
        }

        private static Dictionary<string, string> InvertNames(Dictionary<string, List<string>> nameToIds)
        {
            Dictionary<string, string> idToName = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<string>> pair in nameToIds)
            {
                foreach (string id in pair.Value)
                {
                    idToName[id] = pair.Key;
                }
            }

            return idToName;
        }

        private static string DescribeScope(IReadOnlyList<string> ids, Dictionary<string, string> idToName)
        {
            if (ids.Count == 0)
            {
                return "Model-wide";
            }

            if (ids.Count == 1)
            {
                return NameFor(ids[0], idToName);
            }

            return NameFor(ids[0], idToName) + " -> " + NameFor(ids[1], idToName);
        }

        private static string NameFor(string id, Dictionary<string, string> idToName)
            => idToName.TryGetValue(id, out string? name) ? name : id;

        /// <summary>
        /// Builds the model for a register-bearing export (for example, <c>.tm7</c>), materializing the
        /// full, titled threat register the CLI's <c>tmforge threats --write</c> produces and overlaying
        /// the model's acceptance triage. The canonical read seeds only a sparse (accepted-only) register
        /// from the wire overlay because the register is otherwise regenerable; for a lossless export we
        /// regenerate it in full so the file carries a complete, titled register — accepted risks keep
        /// their state and justification — for tools that consume it, such as the Microsoft Threat
        /// Modeling Tool.
        /// </summary>
        /// <param name="dto">The canonical model.</param>
        /// <returns>The model with its threat register materialized and triaged.</returns>
        private static ThreatModel BuildModelForExport(TmForgeModelDto dto)
        {
            ThreatModel model = BuildModel(dto, out _);

            // BuildModel (through the tmforge-json read) has seeded the durable overlay: manually
            // authored threats in full, plus edited rule threats as sparse patches. Drop the sparse
            // patches so the rules regenerate those threats with their full title and category, but keep
            // the manual threats (which no rule produces), then re-apply the author's edits on top so a
            // lossless export carries a complete, titled register with the author's state and edits.
            List<string> ruleSeeded = model.AllThreatsDictionary.Keys
                .Where(key => !ThreatStateWire.IsManualKey(key))
                .ToList();
            foreach (string key in ruleSeeded)
            {
                model.AllThreatsDictionary.Remove(key);
            }

            using (RuleSet ruleSet = LoadRuleSet())
            {
                if (dto.Analysis != null)
                {
                    ruleSet.Disable(dto.Analysis.DisabledPacks, dto.Analysis.DisabledRuleIds);
                }

                GenerationResult generation = ThreatGenerator.Generate(model, ruleSet);
                ThreatGenerator.Apply(model, generation);
            }

            ApplyOverlayEdits(model, dto.Threats);
            return model;
        }

        /// <summary>
        /// Re-applies the author overlay to the freshly generated register: for each edited rule threat
        /// it layers the author's state, justification, priority, title, description, and mitigation on
        /// top of the rule-generated threat. Manual threats are already materialized by the tmforge-json
        /// read and are left untouched here.
        /// </summary>
        /// <param name="model">The model whose register is edited.</param>
        /// <param name="overlay">The author overlay from the document.</param>
        private static void ApplyOverlayEdits(ThreatModel model, IReadOnlyList<ThreatStateDto>? overlay)
        {
            foreach (ThreatStateDto entry in overlay ?? Array.Empty<ThreatStateDto>())
            {
                if (string.IsNullOrEmpty(entry.Id) ||
                    entry.Manual == true ||
                    !model.AllThreatsDictionary.TryGetValue(entry.Id, out Threat? threat))
                {
                    continue;
                }

                threat.State = ThreatStateWire.Parse(entry.State);
                if (!string.IsNullOrEmpty(entry.Justification))
                {
                    threat.StateInformation = entry.Justification;
                }

                if (!string.IsNullOrEmpty(entry.Priority))
                {
                    threat.Priority = entry.Priority;
                }

                if (!string.IsNullOrEmpty(entry.Title))
                {
                    threat.Title = entry.Title;
                }

                if (!string.IsNullOrEmpty(entry.Description))
                {
                    threat.UserThreatDescription = entry.Description;
                }

                if (!string.IsNullOrEmpty(entry.Mitigation))
                {
                    threat.Properties ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    threat.Properties["Mitigation"] = entry.Mitigation!;
                }

                threat.ModifiedAt = DateTime.UtcNow;
            }
        }

        private static ThreatModel BuildModel(TmForgeModelDto dto, out Dictionary<string, List<string>> nameToIds)
        {
            nameToIds = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            // For a multi-page model the top-level elements/flows mirror the first page, so index the
            // pages when present (and only then) to avoid double-counting page one.
            if (dto.Diagrams != null && dto.Diagrams.Count > 0)
            {
                foreach (TmForgeDiagramDto page in dto.Diagrams)
                {
                    foreach (TmForgeElementDto element in page.Elements ?? Array.Empty<TmForgeElementDto>())
                    {
                        AddName(nameToIds, element.Name, element.Id);
                    }

                    foreach (TmForgeFlowDto flow in page.Flows ?? Array.Empty<TmForgeFlowDto>())
                    {
                        AddName(nameToIds, flow.Name, flow.Id);
                    }
                }
            }
            else
            {
                foreach (TmForgeElementDto element in dto.Elements ?? Array.Empty<TmForgeElementDto>())
                {
                    AddName(nameToIds, element.Name, element.Id);
                }

                foreach (TmForgeFlowDto flow in dto.Flows ?? Array.Empty<TmForgeFlowDto>())
                {
                    AddName(nameToIds, flow.Name, flow.Id);
                }
            }

            // Build the engine model through the canonical tmforge-json format provider so the
            // DTO -> ThreatModel mapping (including custom properties) lives in exactly one place.
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(dto, CanonicalJsonOptions);
            using (MemoryStream stream = new MemoryStream(json))
            {
                return new TmForgeJsonFormat().Read(stream);
            }
        }

        private static TmForgeModelDto ToDto(ThreatModel model)
        {
            using (MemoryStream output = new MemoryStream())
            {
                new TmForgeJsonFormat().Write(model, output);
                return JsonSerializer.Deserialize<TmForgeModelDto>(output.ToArray(), CanonicalJsonOptions)
                    ?? new TmForgeModelDto();
            }
        }

        private static void AddName(Dictionary<string, List<string>> map, string? name, string id)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            if (!map.TryGetValue(name!, out List<string>? ids))
            {
                ids = new List<string>();
                map[name!] = ids;
            }

            ids.Add(id);
        }

        private static IReadOnlyList<string> ResolveIds(string? entity, Dictionary<string, List<string>> nameToIds)
        {
            if (string.IsNullOrEmpty(entity))
            {
                return Array.Empty<string>();
            }

            if (nameToIds.TryGetValue(entity!, out List<string>? exact))
            {
                return exact;
            }

            // The engine's entity text embeds the element name (for example,
            // "Edge [HTTPS request of unknown type and ID=...]"), so fall back to a contains match
            // so the canvas can still highlight the offending element.
            List<string> matches = new List<string>();
            foreach (KeyValuePair<string, List<string>> pair in nameToIds)
            {
                if (pair.Key.Length > 0 && entity!.IndexOf(pair.Key, StringComparison.Ordinal) >= 0)
                {
                    matches.AddRange(pair.Value);
                }
            }

            return matches;
        }

        private static RuleSet LoadRuleSet()
        {
            return AnalysisRuleSources.Create();
        }

        private static string MapSeverity(MessageSeverity severity)
        {
            switch (severity)
            {
                case MessageSeverity.Error:
                    return "error";
                case MessageSeverity.Warning:
                    return "warning";
                default:
                    return "info";
            }
        }

        private static FormatDto MapFormat(IThreatModelFormat format)
        {
            return new FormatDto
            {
                Id = format.Id,
                DisplayName = format.DisplayName,
                Extensions = format.Extensions,
                CanRead = format.Capabilities.CanRead,
                CanWrite = format.Capabilities.CanWrite,
            };
        }
    }
}
