namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using ThreatModelForge.Analysis;
    using ThreatModelForge.Formats;
    using ThreatModelForge.Model;

    /// <summary>
    /// Implements <c>tmforge threats</c>: the persistable, lifecycle-bearing view of the validation
    /// findings. It runs the same rules as <c>analyze</c>, frames each threat-bearing finding as a STRIDE
    /// threat against its element or flow, and — with <c>--write</c> — persists them into the model's
    /// register, preserving any prior triage. Detection is entirely the rules; extend coverage by
    /// adding a rule to a rule pack (there is no separate threat catalog).
    /// </summary>
    internal static class ThreatsCommand
    {
        /// <summary>
        /// Runs the threats command.
        /// </summary>
        /// <param name="args">The command arguments (after the verb).</param>
        /// <returns>Zero on success; a non-zero value on error.</returns>
        public static int Run(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            CliArgs parsed = CliArgs.Parse(args, new[] { "rules" }, new[] { "write" });
            if (parsed.Help)
            {
                PrintUsage();
                return 0;
            }

            if (parsed.UnknownFlags.Count > 0)
            {
                Console.Error.WriteLine("Unknown option: " + parsed.UnknownFlags[0]);
                PrintUsage();
                return 1;
            }

            string? input = parsed.Positionals.Count > 0 ? parsed.Positionals[0] : null;
            if (string.IsNullOrEmpty(input))
            {
                PrintUsage();
                return 1;
            }

            if (!File.Exists(input))
            {
                Console.Error.WriteLine("File not found: " + input);
                return 1;
            }

            (ThreatModel model, IThreatModelFormat? format) = CliModelLoader.Load(input!);
            using RuleSet ruleSet = AnalysisRuleSources.Create(RuleSourceCli.FromPath(parsed.Get("rules")));
            GenerationResult result = ThreatGenerator.Generate(model, ruleSet);

            ApplyResult? written = null;
            if (parsed.HasFlag("write"))
            {
                if (format == null || !format.Capabilities.CanWrite)
                {
                    Console.Error.WriteLine("The model's format does not support writing; omit --write to preview.");
                    return 1;
                }

                written = ThreatGenerator.Apply(model, result);
                AuthoringSupport.Save(model, input!, format);
            }

            if (parsed.Json)
            {
                WriteJson(result, written);
            }
            else
            {
                WriteHuman(result, written, input!);
            }

            return 0;
        }

        private static void WriteJson(GenerationResult result, ApplyResult? written)
        {
            object data = new
            {
                summary = new
                {
                    count = result.Count,
                    written = written == null
                        ? null
                        : new { added = written.Added, updated = written.Updated, preserved = written.Preserved },
                },
                threats = result.Threats.Select(t => new
                {
                    id = t.Id,
                    ruleId = t.RuleId,
                    category = t.Category.ToString(),
                    title = t.Title,
                    mitigation = t.Mitigation,
                    severity = t.Severity,
                    priority = t.Priority,
                    references = t.References.Select(r => new { catalog = r.Catalog, id = r.Id, url = r.Url }).ToList(),
                    scope = t.IsFlowScoped ? "flow" : "element",
                    source = t.SourceName,
                    target = t.TargetName,
                    flow = t.FlowName,
                    sourceId = t.SourceGuid,
                    targetId = t.TargetGuid,
                    flowId = t.FlowGuid,
                    diagramId = t.DiagramGuid,
                    interaction = t.InteractionString,
                }).ToList(),
            };
            CliJson.WriteEnvelope("threats", data);
        }

        private static void WriteHuman(GenerationResult result, ApplyResult? written, string input)
        {
            Console.WriteLine("Threats: " + result.Count + " (from validation findings)");
            if (written != null)
            {
                Console.WriteLine("Wrote:   " + written.Added + " added, " + written.Updated + " updated, " + written.Preserved + " preserved (triaged) in " + input);
            }

            if (result.Count == 0)
            {
                Console.WriteLine();
                Console.WriteLine("No threats detected. Run 'tmforge analyze' for the same findings without persisting them.");
                return;
            }

            Console.WriteLine();
            foreach (GeneratedThreat threat in result.Threats)
            {
                PrintThreat(threat);
            }
        }

        private static void PrintThreat(GeneratedThreat threat)
        {
            string flow = threat.IsFlowScoped && !string.IsNullOrWhiteSpace(threat.FlowName)
                ? " [" + threat.FlowName + "]"
                : string.Empty;
            Console.WriteLine(
                "  [" + CategoryLetter(threat.Category) + "] " + threat.RuleId + "  " +
                threat.InteractionString + flow + "  —  " + threat.Title);
            if (!string.IsNullOrWhiteSpace(threat.Mitigation))
            {
                Console.WriteLine("      fix:  " + threat.Mitigation);
            }

            if (threat.References.Count > 0)
            {
                Console.WriteLine("      refs: " + string.Join(", ", threat.References.Select(r => r.Id)));
            }
        }

        private static string CategoryLetter(StrideCategory category)
        {
            return category switch
            {
                StrideCategory.Spoofing => "S",
                StrideCategory.Tampering => "T",
                StrideCategory.Repudiation => "R",
                StrideCategory.InformationDisclosure => "I",
                StrideCategory.DenialOfService => "D",
                StrideCategory.ElevationOfPrivilege => "E",
                _ => "?",
            };
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Report the model's threats — the persistable, triaged view of the validation findings.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge threats [--write] [--json] [--rules <path>] <file>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("--write     persist the threats into the model's register (preserves prior triage).");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Detection is the rule set (see 'tmforge analyze'); each threat carries a STRIDE category,");
            Console.Error.WriteLine("the rule's mitigation, and CWE/CAPEC references. After --write, triage with");
            Console.Error.WriteLine("'tmforge list threats' and 'tmforge accept'.");
        }
    }
}
