namespace ThreatModelForge.Cli
{
    using System;
    using System.IO;
    using ThreatModelForge.Engine;
    using ThreatModelForge.Model;

    /// <summary>
    /// Implements <c>tmforge export</c>: emits a declarative JSON manifest from an existing model, so
    /// a model authored any way (Studio, imperative verbs, imported <c>.tm7</c>) becomes a
    /// review-friendly text source that round-trips through <c>tmforge apply</c>.
    /// </summary>
    internal static class ExportCommand
    {
        /// <summary>
        /// Runs the export command.
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

            CliArgs parsed = CliArgs.Parse(args, new[] { "out" });
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

            (ThreatModel model, _) = CliModelLoader.Load(input!);
            Manifest manifest = ManifestSupport.Extract(model);
            string json = ManifestSupport.Serialize(manifest);

            string? output = parsed.Get("out");
            if (!string.IsNullOrEmpty(output))
            {
                File.WriteAllText(output!, json);
            }

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("export", new
                {
                    output = string.IsNullOrEmpty(output) ? null : output,
                    boundaries = manifest.Boundaries?.Count ?? 0,
                    elements = manifest.Elements?.Count ?? 0,
                    flows = manifest.Flows?.Count ?? 0,
                });
                return 0;
            }

            if (string.IsNullOrEmpty(output))
            {
                Console.Out.WriteLine(json);
            }
            else
            {
                Console.Error.WriteLine("Exported manifest to " + output + ".");
            }

            return 0;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Export a model as a declarative JSON manifest (round-trips with 'tmforge apply').");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge export [--out <manifest.json>] [--json] <model>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("If --out is omitted, the manifest is written to standard output.");
        }
    }
}
