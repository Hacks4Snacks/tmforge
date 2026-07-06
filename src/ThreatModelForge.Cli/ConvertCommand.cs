namespace ThreatModelForge.Cli
{
    using System;
    using System.IO;
    using ThreatModelForge.Formats;
    using ThreatModelForge.Model;

    /// <summary>
    /// Implements the <c>tmforge convert</c> command, translating a threat model between any two
    /// registered file formats (for example <c>.tm7</c>, <c>.tmforge.json</c>, <c>.drawio</c>, and
    /// <c>.vsdx</c>). The source format is detected from the input; the target is taken from
    /// <c>--to</c> or inferred from the <c>--out</c> extension.
    /// </summary>
    internal static class ConvertCommand
    {
        /// <summary>
        /// Runs the convert command.
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

            CliArgs parsed = CliArgs.Parse(args, new[] { "to", "out" });
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
            string? targetId = parsed.Get("to");
            string? output = parsed.Get("out");

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

            ThreatModelFormatRegistry registry = ThreatModelFormatRegistry.CreateDefault();

            IThreatModelFormat? target = !string.IsNullOrEmpty(targetId)
                ? registry.FindById(targetId!)
                : !string.IsNullOrEmpty(output) ? registry.FindByExtension(output!) : null;

            if (target == null)
            {
                Console.Error.WriteLine("Specify a target format with --to <format>, or an --out path with a known extension.");
                PrintUsage();
                return 1;
            }

            if (!target.Capabilities.CanWrite)
            {
                Console.Error.WriteLine("Format '" + target.Id + "' does not support writing.");
                return 1;
            }

            ThreatModel model = registry.Load(input!);

            string outputPath = !string.IsNullOrEmpty(output)
                ? output!
                : Path.ChangeExtension(input!, target.Extensions.Count > 0 ? target.Extensions[0] : ".out");

            using (FileStream stream = File.Create(outputPath))
            {
                target.Write(model, stream);
            }

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("convert", new { input, output = outputPath, format = target.Id });
            }
            else
            {
                Console.Error.WriteLine("Converted " + input + " -> " + outputPath + " (" + target.Id + ").");
            }

            return 0;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Threat Model Forge format converter.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge convert [--to <format>] [--out <path>] [--json] <input>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("The target format is taken from --to, or inferred from the --out extension.");
            Console.Error.WriteLine("If --out is omitted, the input name is reused with the target extension.");
            Console.Error.WriteLine("Formats: tm7, tmforge-json, drawio, vsdx.");
        }
    }
}
