namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using ThreatModelForge.Analysis;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Model;

    /// <summary>
    /// Implements the <c>tmforge diff</c> command: a structural, identity-keyed comparison of two
    /// threat models. Elements are matched by their stable id, so re-layout or re-serialization
    /// produces no diff; only added, removed, and modified elements (with per-property changes) are
    /// reported.
    /// </summary>
    /// <remarks>
    /// The structural diff ignores geometry, which is what keeps it quiet when a model is merely
    /// re-laid-out. Trust-boundary containment, however, is derived from geometry, so the diff is
    /// paired with a comparison of what each flow crosses — otherwise dragging a data store out of its
    /// boundary would report nothing at all.
    /// </remarks>
    internal static class DiffCommand
    {
        /// <summary>
        /// Runs the diff command.
        /// </summary>
        /// <param name="args">The command arguments (after the verb).</param>
        /// <returns>Zero on success; a non-zero value on error.</returns>
        public static int Run(string[] args)
        {
            CliArgs parsed = CliArgs.Parse(args, Array.Empty<string>(), new[] { "textconv" });
            if (parsed.Help)
            {
                PrintUsage();
                return 0;
            }

            if (parsed.HasFlag("textconv"))
            {
                return RunTextConv(parsed);
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
            CrossingDifference crossings = BoundaryCrossingDiff.Compare(baseModel, revisedModel);

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("diff", BuildPayload(difference, crossings));
                return 0;
            }

            WriteText(difference, crossings);
            return 0;
        }

        private static int RunTextConv(CliArgs parsed)
        {
            IReadOnlyList<string> positionals = parsed.Positionals;
            if (positionals.Count < 1)
            {
                PrintUsage();
                return 1;
            }

            string path = positionals[0];
            if (!File.Exists(path))
            {
                Console.Error.WriteLine("File not found: " + path);
                return 1;
            }

            (ThreatModel model, _) = CliModelLoader.Load(path);
            Console.Out.Write(RenderCanonical(model));
            return 0;
        }

        private static string RenderCanonical(ThreatModel model)
        {
            IReadOnlyList<ElementDescriptor> descriptors = ModelSnapshot.Capture(model);

            List<Guid> surfaceOrder = new List<Guid>();
            Dictionary<Guid, List<ElementDescriptor>> bySurface = new Dictionary<Guid, List<ElementDescriptor>>();
            Dictionary<Guid, string> surfaceNames = new Dictionary<Guid, string>();
            foreach (ElementDescriptor descriptor in descriptors)
            {
                if (!bySurface.TryGetValue(descriptor.DiagramId, out List<ElementDescriptor>? group))
                {
                    group = new List<ElementDescriptor>();
                    bySurface[descriptor.DiagramId] = group;
                    surfaceNames[descriptor.DiagramId] = descriptor.DiagramName;
                    surfaceOrder.Add(descriptor.DiagramId);
                }

                group.Add(descriptor);
            }

            StringBuilder builder = new StringBuilder();
            builder.Append("threat model\n");
            foreach (Guid surfaceId in surfaceOrder)
            {
                builder.Append("diagram \"").Append(surfaceNames[surfaceId]).Append("\"\n");
                IEnumerable<ElementDescriptor> ordered = bySurface[surfaceId]
                    .OrderBy(element => element.Kind, StringComparer.Ordinal)
                    .ThenBy(element => element.Id.ToString(), StringComparer.Ordinal);
                foreach (ElementDescriptor element in ordered)
                {
                    builder.Append("  ").Append(element.Kind).Append(" \"").Append(element.Name).Append("\"  ").Append(element.Id).Append('\n');
                    foreach (KeyValuePair<string, string> attribute in element.Attributes
                        .Where(pair => pair.Key != ModelSnapshot.NameKey && pair.Key != ModelSnapshot.KindKey)
                        .OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    {
                        builder.Append("    ").Append(attribute.Key).Append('=').Append(attribute.Value).Append('\n');
                    }
                }
            }

            return builder.ToString();
        }

        private static object BuildPayload(ModelDifference difference, CrossingDifference crossings)
        {
            return new
            {
                summary = new
                {
                    added = difference.Added.Count,
                    removed = difference.Removed.Count,
                    modified = difference.Modified.Count,
                    crossingChanges = crossings.Changes.Count,
                },
                added = difference.Added.Select(ToPayload).ToArray(),
                removed = difference.Removed.Select(ToPayload).ToArray(),
                modified = difference.Modified.Select(ToPayload).ToArray(),
                crossings = crossings.Changes.Select(ToPayload).ToArray(),
            };
        }

        private static object ToPayload(CrossingChange change)
        {
            return new
            {
                flowId = change.FlowId,
                flow = change.FlowName,
                diagram = change.DiagramName,
                kind = change.Kind.ToString().ToLowerInvariant(),
                added = change.Added.Select(ToPayload).ToArray(),
                removed = change.Removed.Select(ToPayload).ToArray(),
            };
        }

        private static object ToPayload(CrossedBoundary boundary)
        {
            return new { id = boundary.Id, name = boundary.Name };
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

        private static void WriteText(ModelDifference difference, CrossingDifference crossings)
        {
            if (difference.IsEmpty && crossings.IsEmpty)
            {
                Console.WriteLine("No differences.");
                return;
            }

            WriteSection("Added", "+", difference.Added, includeProperties: false);
            WriteSection("Removed", "-", difference.Removed, includeProperties: false);
            WriteSection("Modified", "~", difference.Modified, includeProperties: true);
            WriteCrossings(crossings);

            Console.WriteLine(
                difference.Added.Count + " added, "
                + difference.Removed.Count + " removed, "
                + difference.Modified.Count + " modified, "
                + crossings.Changes.Count + " with changed boundary crossings.");
        }

        /// <summary>
        /// Writes the flows whose trust-boundary crossings changed. This is reported separately from
        /// the element sections because it is derived from geometry rather than from any stored
        /// property: a flow can appear in no other section and still have started crossing a boundary.
        /// </summary>
        /// <param name="crossings">The crossing difference to render.</param>
        private static void WriteCrossings(CrossingDifference crossings)
        {
            if (crossings.IsEmpty)
            {
                return;
            }

            Console.WriteLine("Boundary crossings:");
            foreach (CrossingChange change in crossings.Changes)
            {
                string page = string.IsNullOrEmpty(change.DiagramName) ? string.Empty : " [" + change.DiagramName + "]";
                string state = change.Kind == ChangeKind.Modified
                    ? string.Empty
                    : " (" + change.Kind.ToString().ToLowerInvariant() + " flow)";
                Console.WriteLine("  ~ flow \"" + change.FlowName + "\"" + page + state + "  " + change.FlowId);
                foreach (CrossedBoundary boundary in change.Added)
                {
                    Console.WriteLine("      + now crosses \"" + boundary.Name + "\"");
                }

                foreach (CrossedBoundary boundary in change.Removed)
                {
                    Console.WriteLine("      - no longer crosses \"" + boundary.Name + "\"");
                }
            }

            Console.WriteLine();
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
            Console.Error.WriteLine("  tmforge diff --textconv <model>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Elements are compared by their stable id, so re-layout or re-serialization produces");
            Console.Error.WriteLine("no diff. Reports added, removed, and modified elements with per-property changes.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Trust-boundary crossings are reported separately, because which boundaries a flow");
            Console.Error.WriteLine("crosses is derived from geometry: moving an element across a boundary changes no");
            Console.Error.WriteLine("stored property, so it would otherwise not show up as a difference at all.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("--textconv prints a canonical, deterministic outline of a single model, for use as a");
            Console.Error.WriteLine("git textconv so 'git diff' renders readable .tm7 changes. Wire it up with:");
            Console.Error.WriteLine("  .gitattributes:  *.tm7 diff=tmforge");
            Console.Error.WriteLine("  git config diff.tmforge.textconv \"tmforge diff --textconv\"");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Identity is preserved in .tm7; other formats may not round-trip element ids.");
        }
    }
}
