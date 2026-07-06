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
            Console.WriteLine("Threats:  " + threatCount);

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
            Console.Error.WriteLine("  tmforge open [--json] <input>");
        }
    }
}
