namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ThreatModelForge.Analysis;
    using ThreatModelForge.Editing;

    /// <summary>
    /// Implements <c>tmforge properties</c>: lists the built-in typed property schema (the same
    /// schema Studio renders and the API serves at <c>GET /v1/property-schema</c>) so an agent can
    /// discover every custom property, its value kind, allowed values, and default without running
    /// <c>analyze</c> first or reading the rule catalog. Optionally filtered to one DFD base by
    /// <c>--base</c>.
    /// </summary>
    internal static class PropertiesCommand
    {
        /// <summary>
        /// Runs the properties command.
        /// </summary>
        /// <param name="args">The command arguments (after the verb).</param>
        /// <returns>Zero on success; a non-zero value on error.</returns>
        public static int Run(string[] args)
        {
            CliArgs parsed = CliArgs.Parse(args, new[] { "base", "rules" }, new[] { "explain" });
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

            IReadOnlyList<string> bases = PropertySchemaCatalog.All
                .Select(descriptor => descriptor.AppliesTo)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            string? baseFilter = parsed.Get("base");
            if (!string.IsNullOrEmpty(baseFilter) &&
                !bases.Contains(baseFilter!, StringComparer.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine("Unknown base: " + baseFilter + ". Expected one of: " + string.Join(", ", bases) + ".");
                return 1;
            }

            IReadOnlyList<PropertyDescriptor> descriptors = string.IsNullOrEmpty(baseFilter)
                ? PropertySchemaCatalog.All
                : PropertySchemaCatalog.For(baseFilter!);

            // The rules own the policy for each property (which rule reads it, at what severity, and
            // which values it flags). Join the pure schema with the loaded rule set at emit time so
            // severities and flagged values are never duplicated in the schema and cannot drift.
            using RuleSet ruleSet = AnalysisRuleSources.Create(RuleSourceCli.FromPath(parsed.Get("rules")));
            PropertyPolicyIndex policy = PropertyPolicyIndex.Build(ruleSet);

            if (parsed.HasFlag("explain"))
            {
                return Explain(descriptors, policy, parsed.Json, bases);
            }

            if (parsed.Json)
            {
                var properties = descriptors.Select(descriptor =>
                {
                    PropertyPolicy entry = policy.For(descriptor.AppliesTo, descriptor.Name);
                    return new
                    {
                        appliesTo = descriptor.AppliesTo,
                        name = descriptor.Name,
                        kind = descriptor.Kind,
                        values = descriptor.Values,
                        @default = descriptor.Default,
                        rules = entry.Rules.Select(rule => new { id = rule.Id, severity = rule.Severity }).ToList(),
                        flagged = entry.Flagged,
                    };
                }).ToList();
                CliJson.WriteEnvelope("properties", new { bases, properties });
                return 0;
            }

            if (descriptors.Count == 0)
            {
                Console.Error.WriteLine("No properties found for base: " + baseFilter + ".");
                return 1;
            }

            string[] headers = { "BASE", "PROPERTY", "KIND", "DEFAULT", "RULES", "VALUES" };
            bool anyFlagged = false;
            List<string[]> rows = new List<string[]>();
            foreach (PropertyDescriptor descriptor in descriptors)
            {
                PropertyPolicy entry = policy.For(descriptor.AppliesTo, descriptor.Name);
                if (entry.Flagged.Count > 0)
                {
                    anyFlagged = true;
                }

                rows.Add(new[]
                {
                    descriptor.AppliesTo,
                    descriptor.Name,
                    descriptor.Kind,
                    descriptor.Default,
                    entry.Rules.Count > 0 ? string.Join(",", entry.Rules.Select(rule => rule.Id)) : "-",
                    FormatValues(descriptor, entry),
                });
            }

            Console.Out.WriteLine(TextTable.Render(headers, rows));
            if (anyFlagged)
            {
                Console.Out.WriteLine();
                Console.Out.WriteLine("* value is flagged by a rule (see RULES); run 'tmforge properties --json' for severities.");
            }

            return 0;
        }

        private static int Explain(IReadOnlyList<PropertyDescriptor> descriptors, PropertyPolicyIndex policy, bool json, IReadOnlyList<string> bases)
        {
            List<(string AppliesTo, string Property, string Value, string Rule, string Severity)> rows =
                new List<(string, string, string, string, string)>();
            foreach (PropertyDescriptor descriptor in descriptors)
            {
                PropertyPolicy entry = policy.For(descriptor.AppliesTo, descriptor.Name);
                foreach (PropertyPolicy.RuleValueBinding binding in entry.Bindings)
                {
                    if (binding.Flagged.Count > 0)
                    {
                        foreach (string value in binding.Flagged)
                        {
                            rows.Add((descriptor.AppliesTo, descriptor.Name, value, binding.Id, binding.Severity));
                        }
                    }
                    else
                    {
                        rows.Add((descriptor.AppliesTo, descriptor.Name, "(unset/condition)", binding.Id, binding.Severity));
                    }
                }
            }

            if (json)
            {
                var explain = rows.Select(row => new
                {
                    appliesTo = row.AppliesTo,
                    property = row.Property,
                    value = row.Value,
                    rule = row.Rule,
                    severity = row.Severity,
                }).ToList();
                CliJson.WriteEnvelope("properties", new { bases, explain });
                return 0;
            }

            if (rows.Count == 0)
            {
                Console.Error.WriteLine("No rules read the selected properties, so there is nothing to explain.");
                return 1;
            }

            string[] headers = { "BASE", "PROPERTY", "VALUE", "RULE", "SEVERITY" };
            List<string[]> tableRows = rows
                .Select(row => new[] { row.AppliesTo, row.Property, row.Value, row.Rule, row.Severity })
                .ToList();
            Console.Out.WriteLine(TextTable.Render(headers, tableRows));
            Console.Out.WriteLine();
            Console.Out.WriteLine("Setting a property to a listed VALUE triggers the given RULE at that severity.");
            Console.Out.WriteLine("VALUE '(unset/condition)' means the rule fires when the property is absent or by a computed condition.");
            return 0;
        }

        private static string FormatValues(PropertyDescriptor descriptor, PropertyPolicy policy)
        {
            if (descriptor.Values.Count == 0)
            {
                return "(free text)";
            }

            IEnumerable<string> rendered = descriptor.Values
                .Select(value => policy.Flagged.Contains(value, StringComparer.OrdinalIgnoreCase) ? value + "*" : value);
            return string.Join(" | ", rendered);
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("List the built-in typed property schema (custom properties the analyzer reads and Studio edits).");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge properties [--base <process|datastore|external|flow>] [--explain] [--json] [--rules <path>]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("--explain maps each property VALUE to the rule ID and severity it triggers, so you can");
            Console.Error.WriteLine("predict analyze behavior before running 'tmforge analyze'.");
        }
    }
}
