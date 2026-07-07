namespace ThreatModelForge.Wasm
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Runtime.InteropServices.JavaScript;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using ThreatModelForge.Analysis;
    using ThreatModelForge.Formats;
    using ThreatModelForge.Model;

    /// <summary>
    /// The in-browser engine interop surface (ENH-0002). These <c>[JSExport]</c> methods run the SAME
    /// engine libraries inside the .NET WebAssembly runtime that the <c>/v1</c> API uses server-side,
    /// with no network. Bytes cross the JS boundary as base64 strings to keep marshaling trivial.
    /// </summary>
    public static partial class Engine
    {
        private static readonly JsonSerializerOptions CanonicalJsonOptions = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true,
        };

        private static readonly JsonSerializerOptions FindingsJsonOptions = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// Sanity check that the module loaded and interop works.
        /// </summary>
        /// <returns>The engine runtime banner.</returns>
        [JSExport]
        public static string Ping()
            => $".NET {Environment.Version} WASM engine ready";

        /// <summary>
        /// Runs the real analysis rule set (ThreatModelForge.Analysis.Rules) over a tmforge-json model.
        /// </summary>
        /// <param name="tmforgeJson">The canonical tmforge-json model.</param>
        /// <returns>The findings as a JSON array (camelCase, matching the /v1 FindingDto shape).</returns>
        [JSExport]
        public static string Validate(string tmforgeJson)
        {
            List<Finding> findings = new List<Finding>();
            try
            {
                ThreatModel model = ReadCanonical(tmforgeJson);
                Dictionary<string, List<string>> nameToIds = BuildNameIndex(tmforgeJson);

                using RuleSet ruleSet = LoadRuleSet();
                CollectingMessageWriter writer = new CollectingMessageWriter();
                RuleEvaluationContext context = new RuleEvaluationContext(model, writer);
                ruleSet.Evaluate(context);
                ModelReport report = context.GenerateReport(ruleSet);

                int sequence = 0;
                foreach (RuleReport ruleReport in report.RuleReports)
                {
                    foreach (RuleReportMessage message in ruleReport.Messages)
                    {
                        findings.Add(new Finding
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
            catch (Exception ex)
            {
                findings.Add(new Finding
                {
                    Id = "engine-error",
                    Severity = "warning",
                    Message = $"Engine analysis failed: {ex.Message}",
                    ElementIds = Array.Empty<string>(),
                });
            }

            return JsonSerializer.Serialize(findings, FindingsJsonOptions);
        }

        /// <summary>
        /// Serializes a tmforge-json model to lossless <c>.tm7</c> bytes via the real engine
        /// (<c>DataContractSerializer</c>), returned as base64.
        /// </summary>
        /// <param name="tmforgeJson">The canonical tmforge-json model.</param>
        /// <returns>The <c>.tm7</c> document bytes, base64-encoded.</returns>
        [JSExport]
        public static string WriteTm7(string tmforgeJson)
        {
            ThreatModel model = ReadCanonical(tmforgeJson);
            using MemoryStream stream = new MemoryStream();
            model.Save(stream);
            return Convert.ToBase64String(stream.ToArray());
        }

        /// <summary>
        /// Reads a <c>.tm7</c> document (base64) through the format registry and projects it onto the
        /// canonical tmforge-json model.
        /// </summary>
        /// <param name="tm7Base64">The <c>.tm7</c> document bytes, base64-encoded.</param>
        /// <returns>The canonical tmforge-json model.</returns>
        [JSExport]
        public static string ReadTm7(string tm7Base64)
        {
            byte[] bytes = Convert.FromBase64String(tm7Base64);
            ThreatModelFormatRegistry registry = ThreatModelFormatRegistry.CreateDefault();
            using MemoryStream input = new MemoryStream(bytes);
            ThreatModel model = registry.Load(input, "tm7");
            return WriteCanonical(model);
        }

        /// <summary>
        /// Loads a <c>.tm7</c> and re-saves it straight through the <c>DataContractSerializer</c> model
        /// graph (no tmforge-json projection), so callers can assert byte-stability under WASM.
        /// </summary>
        /// <param name="tm7Base64">The <c>.tm7</c> document bytes, base64-encoded.</param>
        /// <returns>The re-serialized <c>.tm7</c> bytes, base64-encoded.</returns>
        [JSExport]
        public static string RoundTripTm7(string tm7Base64)
        {
            byte[] input = Convert.FromBase64String(tm7Base64);
            ThreatModel model;
            using (MemoryStream inStream = new MemoryStream(input))
            {
                model = ThreatModel.Load(inStream);
            }

            using MemoryStream outStream = new MemoryStream();
            model.Save(outStream);
            return Convert.ToBase64String(outStream.ToArray());
        }

        // Reads a tmforge-json string into the engine model via the canonical format provider.
        private static ThreatModel ReadCanonical(string tmforgeJson)
        {
            using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(tmforgeJson));
            return new TmForgeJsonFormat().Read(stream);
        }

        // Writes the engine model back to a tmforge-json string.
        private static string WriteCanonical(ThreatModel model)
        {
            using MemoryStream output = new MemoryStream();
            new TmForgeJsonFormat().Write(model, output);
            return Encoding.UTF8.GetString(output.ToArray());
        }

        // Loads the default rule set from the reflectively-referenced rules assembly.
        private static RuleSet LoadRuleSet()
            => RuleSet.LoadDefault(new[] { Assembly.Load("ThreatModelForge.Analysis.Rules") });

        private static string MapSeverity(MessageSeverity severity) => severity switch
        {
            MessageSeverity.Error => "error",
            MessageSeverity.Warning => "warning",
            _ => "info",
        };

        // Builds a name -> element/flow id map from the tmforge-json, mirroring EngineService.
        private static Dictionary<string, List<string>> BuildNameIndex(string tmforgeJson)
        {
            Dictionary<string, List<string>> map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            using JsonDocument doc = JsonDocument.Parse(tmforgeJson);
            JsonElement root = doc.RootElement;
            AddNames(map, root, "elements");
            AddNames(map, root, "flows");
            return map;
        }

        private static void AddNames(Dictionary<string, List<string>> map, JsonElement root, string arrayName)
        {
            if (!root.TryGetProperty(arrayName, out JsonElement array) || array.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (JsonElement item in array.EnumerateArray())
            {
                string? name = item.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
                string? id = item.TryGetProperty("id", out JsonElement i) ? i.GetString() : null;
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(id))
                {
                    continue;
                }

                if (!map.TryGetValue(name!, out List<string>? ids))
                {
                    ids = new List<string>();
                    map[name!] = ids;
                }

                ids.Add(id!);
            }
        }

        // Resolves an engine finding entity back to element ids (exact match, then contains).
        private static string[] ResolveIds(string? entity, Dictionary<string, List<string>> nameToIds)
        {
            if (string.IsNullOrEmpty(entity))
            {
                return Array.Empty<string>();
            }

            if (nameToIds.TryGetValue(entity!, out List<string>? exact))
            {
                return exact.ToArray();
            }

            List<string> matches = new List<string>();
            foreach (KeyValuePair<string, List<string>> pair in nameToIds)
            {
                if (pair.Key.Length > 0 && entity!.IndexOf(pair.Key, StringComparison.Ordinal) >= 0)
                {
                    matches.AddRange(pair.Value);
                }
            }

            return matches.ToArray();
        }

        // The wire shape the Studio's engine client reads (id/severity/ruleId/message/elementIds).
        private sealed class Finding
        {
            public string Id { get; set; } = string.Empty;

            public string Severity { get; set; } = "info";

            public string? RuleId { get; set; }

            public string Message { get; set; } = string.Empty;

            public string[] ElementIds { get; set; } = Array.Empty<string>();
        }

        // A no-op writer; findings are read from the generated ModelReport (as in EngineService).
        private sealed class CollectingMessageWriter : MessageWriter
        {
            /// <inheritdoc/>
            public override void WriteCore(MessageSeverity severity, string messageID, string text)
            {
            }
        }
    }
}
