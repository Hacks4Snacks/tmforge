namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using ThreatModelForge.Analysis;
    using ThreatModelForge.Engine;
    using ThreatModelForge.Formats;
    using ThreatModelForge.KnowledgeBase;
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

            CliArgs parsed = CliArgs.Parse(
                args,
                new[] { "rules", "edit", "remove", "title", "category", "scope", "state", "priority", "mitigation", "description", "note" },
                new[] { "write", "add" });
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

            bool add = parsed.HasFlag("add");
            string? editId = parsed.Get("edit");
            string? removeId = parsed.Get("remove");
            if (add || editId != null || removeId != null)
            {
                return RunAuthoring(parsed, input!, add, editId, removeId);
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

        private static int RunAuthoring(CliArgs parsed, string input, bool add, string? editId, string? removeId)
        {
            (ThreatModel model, IThreatModelFormat? format) = CliModelLoader.Load(input);
            if (format == null || !format.Capabilities.CanWrite)
            {
                Console.Error.WriteLine("The model's format does not support writing.");
                return 1;
            }

            // Materialize the rule register so rule threats are present and editable alongside manual
            // threats; existing triage (accepted / edited / manual) is preserved.
            using (RuleSet ruleSet = AnalysisRuleSources.Create(RuleSourceCli.FromPath(parsed.Get("rules"))))
            {
                ThreatGenerator.Apply(model, ThreatGenerator.Generate(model, ruleSet));
            }

            if (add)
            {
                return RunAdd(parsed, input, model, format);
            }

            if (editId != null)
            {
                return RunEdit(parsed, input, model, format, editId);
            }

            return RunRemove(parsed, input, model, format, removeId!);
        }

        private static int RunAdd(CliArgs parsed, string input, ThreatModel model, IThreatModelFormat format)
        {
            string? title = parsed.Get("title");
            string? category = parsed.Get("category");
            if (string.IsNullOrWhiteSpace(title))
            {
                Console.Error.WriteLine("--title is required when adding a threat.");
                return 1;
            }

            if (string.IsNullOrWhiteSpace(category))
            {
                Console.Error.WriteLine("--category is required when adding a threat (a STRIDE category).");
                return 1;
            }

            string? scope = parsed.Get("scope");
            IReadOnlyList<string>? elementIds = string.IsNullOrWhiteSpace(scope) ? null : new[] { scope! };
            Threat threat = ThreatGenerator.AddManual(
                model,
                category!,
                title!,
                elementIds,
                ThreatStateWire.Parse(parsed.Get("state")),
                parsed.Get("priority"),
                parsed.Get("description"),
                parsed.Get("mitigation"));

            AuthoringSupport.Save(model, input, format);

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("threats", new
                {
                    action = "add",
                    id = threat.InteractionKey,
                    category = threat.UserThreatCategory,
                    title = threat.Title,
                    state = ThreatStateWire.ToWire(threat.State),
                    scope,
                });
            }
            else
            {
                Console.Error.WriteLine("Added manual threat " + threat.InteractionKey + " in " + input + ": [" + threat.UserThreatCategory + "] " + threat.Title);
                Console.Error.WriteLine("See it with 'tmforge list threats " + input + "'.");
            }

            return 0;
        }

        private static int RunEdit(CliArgs parsed, string input, ThreatModel model, IThreatModelFormat format, string editId)
        {
            string? stateArg = parsed.Get("state");
            ThreatState? state = stateArg == null ? null : ThreatStateWire.Parse(stateArg);
            if (!ThreatGenerator.Edit(
                model,
                editId,
                state,
                parsed.Get("priority"),
                parsed.Get("description"),
                parsed.Get("mitigation"),
                parsed.Get("note")))
            {
                Console.Error.WriteLine("Threat not found: " + editId + ". Use 'tmforge list threats " + input + "' to find its id.");
                return 1;
            }

            AuthoringSupport.Save(model, input, format);

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("threats", new { action = "edit", id = editId });
            }
            else
            {
                Console.Error.WriteLine("Edited threat " + editId + " in " + input + ".");
            }

            return 0;
        }

        private static int RunRemove(CliArgs parsed, string input, ThreatModel model, IThreatModelFormat format, string removeId)
        {
            if (!ThreatGenerator.Remove(model, removeId))
            {
                Console.Error.WriteLine("No manual threat to remove for: " + removeId + " (rule threats regenerate from the rules; accept or edit them instead).");
                return 1;
            }

            AuthoringSupport.Save(model, input, format);

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("threats", new { action = "remove", id = removeId });
            }
            else
            {
                Console.Error.WriteLine("Removed threat " + removeId + " from " + input + ".");
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
            Console.Error.WriteLine("Report or author the model's threats — the persistable, triaged view of the validation findings.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge threats [--write] [--json] [--rules <path>] <file>");
            Console.Error.WriteLine("  tmforge threats --add --title <t> --category <STRIDE> [--scope <id>] [--state <s>] [--priority <p>] [--mitigation <m>] [--description <d>] [--json] <file>");
            Console.Error.WriteLine("  tmforge threats --edit <id> [--state <s>] [--priority <p>] [--mitigation <m>] [--description <d>] [--note <n>] [--json] <file>");
            Console.Error.WriteLine("  tmforge threats --remove <id> [--json] <file>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("--write     persist the generated threats into the model's register (preserves prior triage).");
            Console.Error.WriteLine("--add       author a manual threat; --category is a STRIDE category (Spoofing / Tampering / Repudiation / InformationDisclosure / DenialOfService / ElevationOfPrivilege).");
            Console.Error.WriteLine("--edit      change a threat's state (Open / NeedsInvestigation / Mitigated / Accepted), priority, mitigation, description, or note.");
            Console.Error.WriteLine("--remove    delete a manual threat (rule threats regenerate; accept or edit them instead).");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Detection is the rule set (see 'tmforge analyze'); each threat carries a STRIDE category,");
            Console.Error.WriteLine("the rule's mitigation, and CWE/CAPEC references. List the register with 'tmforge list threats'.");
        }
    }
}
