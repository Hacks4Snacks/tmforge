namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Model;

    /// <summary>
    /// Implements the <c>tmforge diff</c> command: a structural, identity-keyed comparison of two
    /// threat models. Elements are matched by their stable id, so re-layout or re-serialization
    /// produces no diff; only added, removed, and modified elements (with per-property changes) are
    /// reported.
    /// </summary>
    internal static class DiffCommand
    {
        /// <summary>
        /// Runs the diff command.
        /// </summary>
        /// <param name="args">The command arguments (after the verb).</param>
        /// <returns>Zero on success; a non-zero value on error.</returns>
        public static int Run(string[] args)
        {
            CliArgs parsed = CliArgs.Parse(args, Array.Empty<string>());
            if (parsed.Help)
            {
                PrintUsage();
                return 0;
            }

            IReadOnlyList<string> positionals = parsed.Positionals;
            if (positionals.Count < 2)
            {
                PrintUsage();
                return 1;
            }

            string basePath = positionals[0];
            string revisedPath = positionals[1];

            if (!File.Exists(basePath))
            {
                Console.Error.WriteLine("File not found: " + basePath);
                return 1;
            }

            if (!File.Exists(revisedPath))
            {
                Console.Error.WriteLine("File not found: " + revisedPath);
                return 1;
            }

            (ThreatModel baseModel, _) = CliModelLoader.Load(basePath);
            (ThreatModel revisedModel, _) = CliModelLoader.Load(revisedPath);

            ModelDifference difference = ModelDiff.Compare(baseModel, revisedModel);

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("diff", BuildPayload(difference));
                return 0;
            }

            WriteText(difference);
            return 0;
        }

        private static object BuildPayload(ModelDifference difference)
        {
            return new
            {
                summary = new
                {
                    added = difference.Added.Count,
                    removed = difference.Removed.Count,
                    modified = difference.Modified.Count,
                },
                added = difference.Added.Select(ToPayload).ToArray(),
                removed = difference.Removed.Select(ToPayload).ToArray(),
                modified = difference.Modified.Select(ToPayload).ToArray(),
            };
        }

        private static object ToPayload(ElementChange change)
        {
            return new
            {
                id = change.Id,
                kind = change.Kind,
                element = change.ElementKind,
                name = change.Name,
                diagram = change.DiagramName,
                properties = change.PropertyChanges
                    .Select(property => new { key = property.Key, from = property.From, to = property.To })
                    .ToArray(),
            };
        }

        private static void WriteText(ModelDifference difference)
        {
            if (difference.IsEmpty)
            {
                Console.WriteLine("No differences.");
                return;
            }

            WriteSection("Added", "+", difference.Added, includeProperties: false);
            WriteSection("Removed", "-", difference.Removed, includeProperties: false);
            WriteSection("Modified", "~", difference.Modified, includeProperties: true);

            Console.WriteLine(
                difference.Added.Count + " added, "
                + difference.Removed.Count + " removed, "
                + difference.Modified.Count + " modified.");
        }

        private static void WriteSection(string title, string marker, IReadOnlyList<ElementChange> changes, bool includeProperties)
        {
            if (changes.Count == 0)
            {
                return;
            }

            Console.WriteLine(title + ":");
            foreach (ElementChange change in changes)
            {
                string page = string.IsNullOrEmpty(change.DiagramName) ? string.Empty : " [" + change.DiagramName + "]";
                Console.WriteLine("  " + marker + " " + change.ElementKind + " \"" + change.Name + "\"" + page + "  " + change.Id);
                if (!includeProperties)
                {
                    continue;
                }

                foreach (PropertyChange property in change.PropertyChanges)
                {
                    Console.WriteLine("      " + property.Key + ": " + Format(property.From) + " -> " + Format(property.To));
                }
            }

            Console.WriteLine();
        }

        private static string Format(string? value)
        {
            return value == null ? "(none)" : "\"" + value + "\"";
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Show a structural diff between two threat models, matched by element id.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge diff <base> <revised> [--json]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Elements are compared by their stable id, so re-layout or re-serialization produces");
            Console.Error.WriteLine("no diff. Reports added, removed, and modified elements with per-property changes.");
            Console.Error.WriteLine("Identity is preserved in .tm7; other formats may not round-trip element ids.");
        }
    }
}
