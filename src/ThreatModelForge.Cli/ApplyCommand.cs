namespace ThreatModelForge.Cli
{
    using System;
    using System.IO;
    using System.Text.Json;
    using ThreatModelForge.Formats;
    using ThreatModelForge.Model;

    /// <summary>
    /// Implements <c>tmforge apply</c>: materializes a declarative JSON manifest into a model file in
    /// one shot. The build is transactional — the whole manifest is built in memory and written
    /// atomically, so a bad manifest never leaves a half-built model. Re-running against the same
    /// manifest regenerates the model idempotently.
    /// </summary>
    internal static class ApplyCommand
    {
        /// <summary>
        /// Runs the apply command.
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

            CliArgs parsed = CliArgs.Parse(args, new[] { "out", "format" }, new[] { "force", "dry-run" });
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

            Manifest? manifest;
            try
            {
                manifest = ManifestSupport.Deserialize(File.ReadAllText(input!));
            }
            catch (JsonException ex)
            {
                Console.Error.WriteLine("Invalid manifest JSON: " + ex.Message);
                return 1;
            }

            if (manifest == null)
            {
                Console.Error.WriteLine("The manifest is empty.");
                return 1;
            }

            bool dryRun = parsed.HasFlag("dry-run");
            string outputPath = parsed.Get("out") ?? Path.ChangeExtension(input!, ".tm7");
            string? formatId = parsed.Get("format");
            ThreatModelFormatRegistry registry = ThreatModelFormatRegistry.CreateDefault();
            IThreatModelFormat? format = !string.IsNullOrEmpty(formatId)
                ? registry.FindById(formatId!)
                : registry.FindByExtension(outputPath);
            if (format == null)
            {
                Console.Error.WriteLine("Could not determine the output format for '" + outputPath + "'. Use --format <id> (tm7, tmforge-json, drawio, vsdx).");
                return 1;
            }

            if (!format.Capabilities.CanWrite)
            {
                Console.Error.WriteLine("Format '" + format.Id + "' does not support writing.");
                return 1;
            }

            if (!ManifestSupport.Build(manifest, parsed.HasFlag("force"), out ThreatModel model, out ManifestSummary summary, out string? error))
            {
                Console.Error.WriteLine("apply failed: " + error);
                return 1;
            }

            if (!dryRun)
            {
                AuthoringSupport.Save(model, outputPath, format);
            }

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("apply", new
                {
                    output = dryRun ? null : outputPath,
                    format = format.Id,
                    dryRun,
                    boundaries = summary.Boundaries,
                    elements = summary.Elements,
                    flows = summary.Flows,
                });
            }
            else if (dryRun)
            {
                Console.Error.WriteLine("Manifest is valid: " + summary.Boundaries + " boundaries, " + summary.Elements + " elements, " + summary.Flows + " flows (--dry-run, not written).");
            }
            else
            {
                Console.Error.WriteLine("Applied manifest to " + outputPath + " (" + format.Id + "): " + summary.Boundaries + " boundaries, " + summary.Elements + " elements, " + summary.Flows + " flows.");
            }

            return 0;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Build a model from a declarative JSON manifest (all-or-nothing).");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge apply <manifest.json> [--out <model>] [--format <id>] [--force] [--dry-run] [--json]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("The output defaults to the manifest path with a .tm7 extension; --out or --format override it.");
            Console.Error.WriteLine("--dry-run validates the manifest without writing; --force stores unknown property values.");
            Console.Error.WriteLine("Round-trips with 'tmforge export'. Elements are referenced by alias (or unique name).");
        }
    }
}
