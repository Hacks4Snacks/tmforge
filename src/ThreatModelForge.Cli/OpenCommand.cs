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
    /// Implements the <c>tmforge open</c> command: a read-only summary of a threat model — its
    /// metadata, per-diagram element counts, and totals (including threats).
    /// </summary>
    internal static class OpenCommand
    {
        /// <summary>
        /// Runs the open command.
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

            bool json = false;
            string? input = null;
            string? rulePath = null;
            for (int index = 0; index < args.Length; index++)
            {
                string arg = args[index];
                if (string.Equals(arg, "-?", StringComparison.Ordinal) || string.Equals(arg, "--help", StringComparison.Ordinal))
                {
                    PrintUsage();
                    return 0;
                }

                if (string.Equals(arg, "--json", StringComparison.Ordinal))
                {
                    json = true;
                }
                else if (string.Equals(arg, "--" + RuleSourceCli.OptionName, StringComparison.Ordinal))
                {
                    if (index + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("--" + RuleSourceCli.OptionName + " requires a path.");
                        return 1;
                    }

                    rulePath = args[++index];
                }
                else if (!arg.StartsWith("-", StringComparison.Ordinal))
                {
                    input = arg;
                }
            }

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

            List<DiagramSummary> summaries = model.DrawingSurfaceList
                .Select(DiagramSummary.FromDrawingSurfaceModel)
                .ToList();

            int componentCount = summaries.Sum(s => s.ComponentCount);
            int connectorCount = summaries.Sum(s => s.ConnectorCount);
            int trustBoundaryCount = summaries.Sum(s => s.TrustBoundaryCount);
            int threatCount = model.AllThreatsDictionary.Count;

            // Classify the register against one generation run, so the split counts and the model they
            // describe cannot disagree.
            using RuleSet ruleSet = AnalysisRuleSources.Create(RuleSourceCli.FromPath(rulePath));
            ThreatRegisterSummary register = ThreatRegisterClassifier.Classify(
                model,
                ThreatGenerator.Generate(model, ruleSet),
                ruleSet);

            string name = !string.IsNullOrWhiteSpace(model.MetaInformation?.ThreatModelName)
                ? model.MetaInformation!.ThreatModelName!
                : Path.GetFileNameWithoutExtension(input!);
            string owner = model.MetaInformation?.Owner ?? string.Empty;

            if (json)
            {
                object data = new
                {
                    name,
                    owner,
                    source = input,
                    format = new { id = format?.Id, name = format?.DisplayName },
                    diagramCount = summaries.Count,
                    componentCount,
                    connectorCount,
                    trustBoundaryCount,
                    threatCount,
                    threats = new
                    {
                        total = threatCount,
                        manual = register.Manual,
                        persistedGenerated = register.PersistedGenerated,
                        currentGenerated = register.CurrentGenerated,
                        staleGenerated = register.StaleGenerated,
                        indeterminateGenerated = register.IndeterminateGenerated,
                        unavailableRuleIds = register.UnavailableRuleIds,
                    },
                    diagrams = summaries.Select(s => new
                    {
                        id = s.ID,
                        header = s.Header,
                        componentCount = s.ComponentCount,
                        connectorCount = s.ConnectorCount,
                        trustBoundaryCount = s.TrustBoundaryCount,
                    }),
                };
                CliJson.WriteEnvelope("open", data);
                return 0;
            }

            Console.WriteLine("Model:    " + (string.IsNullOrEmpty(name) ? "(untitled)" : name));
            Console.WriteLine("Owner:    " + (string.IsNullOrEmpty(owner) ? "(none)" : owner));
            Console.WriteLine("Source:   " + input);
            Console.WriteLine("Format:   " + (format != null ? format.DisplayName + " (" + format.Id + ")" : "(unknown)"));
            Console.WriteLine("Diagrams: " + summaries.Count);
            Console.WriteLine("Elements: " + componentCount + " components, " + connectorCount + " flows, " + trustBoundaryCount + " trust boundaries");
            Console.WriteLine("Threats:  " + threatCount + " in the register (" + register.Manual + " manual, " + register.PersistedGenerated + " generated)");
            Console.WriteLine("          " + register.CurrentGenerated + " generated by the current rules");
            if (register.StaleGenerated > 0)
            {
                Console.WriteLine("          " + register.StaleGenerated + " stale (the rule no longer fires; inspect with 'tmforge list threats')");
            }

            if (register.IndeterminateGenerated > 0)
            {
                Console.WriteLine(
                    "          " + register.IndeterminateGenerated + " could not be checked \u2014 not in this run's rules: " +
                    string.Join(", ", register.UnavailableRuleIds));
            }

            if (summaries.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Diagrams:");
                foreach (DiagramSummary summary in summaries)
                {
                    string header = string.IsNullOrEmpty(summary.Header) ? "(untitled diagram)" : summary.Header!;
                    Console.WriteLine("  " + header + ": " + summary.ComponentCount + " components, " + summary.ConnectorCount + " flows, " + summary.TrustBoundaryCount + " trust boundaries");
                }
            }

            return 0;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Summarize a threat model.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge open [--json] [--rules <path>] <input>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Threat counts are split by origin and standing: manual, generated by the current");
            Console.Error.WriteLine("rules, stored in the register, and stale (stored but no longer produced). An entry");
            Console.Error.WriteLine("whose rule is absent or disabled is reported separately, never assumed stale.");
            Console.Error.WriteLine("--rules  evaluate with a custom rule bundle, so its threats are recognized too.");
        }
    }
}
