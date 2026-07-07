namespace ThreatModelForge.Engine
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Text;
    using System.Text.Json;
    using ThreatModelForge.Analysis;
    using ThreatModelForge.Analysis.Rules;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Formats;
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
        public static IReadOnlyList<FindingDto> Validate(TmForgeModelDto dto)
        {
            List<FindingDto> findings = new List<FindingDto>();
            try
            {
                ThreatModel model = BuildModel(dto, out Dictionary<string, List<string>> nameToIds);
                using (RuleSet ruleSet = LoadRuleSet())
                {
                    if (dto.Validation != null)
                    {
                        ruleSet.Disable(dto.Validation.DisabledPacks, dto.Validation.DisabledRuleIds);
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
        /// Serializes the supplied model to lossless <c>.tm7</c> bytes via the real engine.
        /// </summary>
        /// <param name="dto">The canonical model.</param>
        /// <returns>The <c>.tm7</c> document bytes.</returns>
        public static byte[] ExportTm7(TmForgeModelDto dto)
        {
            ThreatModel model = BuildModel(dto, out _);
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

            using (MemoryStream output = new MemoryStream())
            {
                new TmForgeJsonFormat().Write(model, output);
                return JsonSerializer.Deserialize<TmForgeModelDto>(output.ToArray(), CanonicalJsonOptions)
                    ?? new TmForgeModelDto();
            }
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

            ThreatModel model = BuildModel(dto, out _);
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
                DiagramSvgRenderer renderer = new DiagramSvgRenderer();
                string svg = model.DrawingSurfaceList.Count > 0
                    ? renderer.Render(model.DrawingSurfaceList[0]).ToString()
                    : "<svg xmlns=\"http://www.w3.org/2000/svg\" />";
                return Encoding.UTF8.GetBytes(svg);
            }

            string html = new HtmlReportWriter().Write(model);
            return Encoding.UTF8.GetBytes(html);
        }

        private static ThreatModel BuildModel(TmForgeModelDto dto, out Dictionary<string, List<string>> nameToIds)
        {
            nameToIds = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (TmForgeElementDto element in dto.Elements ?? Array.Empty<TmForgeElementDto>())
            {
                AddName(nameToIds, element.Name, element.Id);
            }

            foreach (TmForgeFlowDto flow in dto.Flows ?? Array.Empty<TmForgeFlowDto>())
            {
                AddName(nameToIds, flow.Name, flow.Id);
            }

            // Build the engine model through the canonical tmforge-json format provider so the
            // DTO -> ThreatModel mapping (including custom properties) lives in exactly one place.
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(dto, CanonicalJsonOptions);
            using (MemoryStream stream = new MemoryStream(json))
            {
                return new TmForgeJsonFormat().Read(stream);
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
            return RuleSet.LoadDefault(new[] { Assembly.Load("ThreatModelForge.Analysis.Rules") });
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
