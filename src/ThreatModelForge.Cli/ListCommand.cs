namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using ThreatModelForge.Analysis;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Formats;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Implements the <c>tmforge list</c> command: read-only listings of a model's components,
    /// flows, trust boundaries, threats, or diagrams.
    /// </summary>
    internal static class ListCommand
    {
        /// <summary>
        /// Runs the list command.
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
            string? noun = null;
            string? input = null;
            foreach (string arg in args)
            {
                if (string.Equals(arg, "-?", StringComparison.Ordinal) || string.Equals(arg, "--help", StringComparison.Ordinal))
                {
                    PrintUsage();
                    return 0;
                }

                if (string.Equals(arg, "--json", StringComparison.Ordinal))
                {
                    json = true;
                }
                else if (!arg.StartsWith("-", StringComparison.Ordinal))
                {
                    if (noun == null)
                    {
                        noun = arg;
                    }
                    else if (input == null)
                    {
                        input = arg;
                    }
                }
            }

            if (string.IsNullOrEmpty(noun) || string.IsNullOrEmpty(input))
            {
                PrintUsage();
                return 1;
            }

            string? kind = NormalizeNoun(noun!);
            if (kind == null)
            {
                Console.Error.WriteLine("Unknown list type: " + noun);
                PrintUsage();
                return 1;
            }

            if (!File.Exists(input))
            {
                Console.Error.WriteLine("File not found: " + input);
                return 1;
            }

            (ThreatModel model, _) = CliModelLoader.Load(input!);

            string[] headers;
            List<string[]> rows;
            object items;
            switch (kind)
            {
                case "components":
                    BuildComponents(model, out headers, out rows, out items);
                    break;
                case "flows":
                    BuildFlows(model, out headers, out rows, out items);
                    break;
                case "boundaries":
                    BuildBoundaries(model, out headers, out rows, out items);
                    break;
                case "diagrams":
                    BuildDiagrams(model, out headers, out rows, out items);
                    break;
                default:
                    BuildThreats(model, out headers, out rows, out items);
                    break;
            }

            if (json)
            {
                object data = new
                {
                    kind,
                    count = rows.Count,
                    items,
                };
                CliJson.WriteEnvelope("list", data);
                return 0;
            }

            if (rows.Count == 0)
            {
                Console.WriteLine("No " + kind + " found.");
                return 0;
            }

            Console.WriteLine(TextTable.Render(headers, rows));
            return 0;
        }

        private static string? NormalizeNoun(string noun)
        {
            switch (noun.ToLowerInvariant())
            {
                case "component":
                case "components":
                case "entity":
                case "entities":
                    return "components";
                case "flow":
                case "flows":
                case "connector":
                case "connectors":
                    return "flows";
                case "boundary":
                case "boundaries":
                case "trustboundary":
                case "trustboundaries":
                    return "boundaries";
                case "diagram":
                case "diagrams":
                    return "diagrams";
                case "threat":
                case "threats":
                    return "threats";
                default:
                    return null;
            }
        }

        private static ModelListing GenerateListing(ThreatModel model)
        {
            RuleEvaluationContext context = new RuleEvaluationContext(model, new NullMessageWriter());
            return context.GenerateListing();
        }

        private static string? StencilLabel(ThreatModel model, Guid id)
        {
            Entity? element = model.DrawingSurfaceList
                .Select(diagram => DiagramEditor.FindElement(diagram, id))
                .FirstOrDefault(e => e != null);
            if (element == null)
            {
                return null;
            }

            IReadOnlyDictionary<string, string> properties = DiagramElementHelper.GetCustomProperties(element);
            return properties.TryGetValue("StencilType", out string? stencilType)
                ? StencilCatalog.Find(stencilType)?.Label
                : null;
        }

        private static void BuildComponents(ThreatModel model, out string[] headers, out List<string[]> rows, out object items)
        {
            ModelListing listing = GenerateListing(model);
            headers = new[] { "NAME", "TYPE", "EXTERNAL", "DIAGRAM", "ID" };
            rows = new List<string[]>();
            foreach (ComponentListing component in listing.Components)
            {
                rows.Add(new[]
                {
                    component.Name ?? string.Empty,
                    StencilLabel(model, component.ID) ?? component.HeaderName ?? component.TypeID ?? string.Empty,
                    component.IsExternalInteractor ? "yes" : string.Empty,
                    component.DiagramHeader ?? string.Empty,
                    component.ID.ToString(),
                });
            }

            items = listing.Components;
        }

        private static void BuildFlows(ThreatModel model, out string[] headers, out List<string[]> rows, out object items)
        {
            ModelListing listing = GenerateListing(model);
            headers = new[] { "NAME", "SOURCE", "TARGET", "DIAGRAM", "ID" };
            rows = new List<string[]>();
            foreach (ConnectorListing connector in listing.Connectors)
            {
                rows.Add(new[]
                {
                    connector.Name ?? string.Empty,
                    connector.SourceComponentID.ToString(),
                    connector.TargetComponentID.ToString(),
                    connector.DiagramHeader ?? string.Empty,
                    connector.ID.ToString(),
                });
            }

            items = listing.Connectors;
        }

        private static void BuildBoundaries(ThreatModel model, out string[] headers, out List<string[]> rows, out object items)
        {
            ModelListing listing = GenerateListing(model);
            headers = new[] { "NAME", "TYPE", "DIAGRAM", "ID" };
            rows = new List<string[]>();
            foreach (EntityListing boundary in listing.TrustBoundaries)
            {
                rows.Add(new[]
                {
                    boundary.Name ?? string.Empty,
                    boundary.HeaderName ?? boundary.TypeID ?? string.Empty,
                    boundary.DiagramHeader ?? string.Empty,
                    boundary.ID.ToString(),
                });
            }

            items = listing.TrustBoundaries;
        }

        private static void BuildDiagrams(ThreatModel model, out string[] headers, out List<string[]> rows, out object items)
        {
            List<DiagramSummary> summaries = model.DrawingSurfaceList
                .Select(DiagramSummary.FromDrawingSurfaceModel)
                .ToList();
            headers = new[] { "HEADER", "COMPONENTS", "FLOWS", "BOUNDARIES", "ID" };
            rows = new List<string[]>();
            foreach (DiagramSummary summary in summaries)
            {
                rows.Add(new[]
                {
                    summary.Header ?? string.Empty,
                    summary.ComponentCount.ToString(),
                    summary.ConnectorCount.ToString(),
                    summary.TrustBoundaryCount.ToString(),
                    summary.ID.ToString(),
                });
            }

            items = summaries.Select(s => new
            {
                id = s.ID,
                header = s.Header,
                componentCount = s.ComponentCount,
                connectorCount = s.ConnectorCount,
                trustBoundaryCount = s.TrustBoundaryCount,
            }).ToList();
        }

        private static void BuildThreats(ThreatModel model, out string[] headers, out List<string[]> rows, out object items)
        {
            List<Threat> threats = model.AllThreatsDictionary.Values.ToList();
            headers = new[] { "TITLE", "PRIORITY", "STATE", "CATEGORY", "ID" };
            rows = new List<string[]>();
            foreach (Threat threat in threats)
            {
                rows.Add(new[]
                {
                    threat.Title ?? string.Empty,
                    threat.Priority ?? string.Empty,
                    ThreatStateWire.ToWire(threat.State),
                    threat.UserThreatCategory ?? string.Empty,
                    threat.Id.ToString(),
                });
            }

            items = threats.Select(t => new
            {
                id = t.Id,
                title = t.Title,
                priority = t.Priority,
                state = ThreatStateWire.ToWire(t.State),
                category = t.UserThreatCategory,
                flowGuid = t.FlowGuid,
                diagramGuid = t.DrawingSurfaceGuid,
            }).ToList();
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("List parts of a threat model.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge list <components|flows|boundaries|threats|diagrams> [--json] <input>");
        }
    }
}
