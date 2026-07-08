namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// Implements <c>tmforge schema</c>: describes the machine-readable <c>--json</c> envelope and the
    /// per-command <c>data</c> payload shapes, so automation can be written against a documented
    /// contract instead of shapes discovered by probing output.
    /// </summary>
    internal static class SchemaCommand
    {
        /// <summary>
        /// Runs the schema command.
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

            if (parsed.UnknownFlags.Count > 0)
            {
                Console.Error.WriteLine("Unknown option: " + parsed.UnknownFlags[0]);
                PrintUsage();
                return 1;
            }

            if (parsed.Json)
            {
                List<object> commands = CommandCatalog.Commands
                    .Where(entry => entry.JsonData != null)
                    .Select(entry => (object)new { command = entry.Verb, data = entry.JsonData })
                    .ToList();
                CliJson.WriteEnvelope("schema", new
                {
                    envelope = new
                    {
                        schemaVersion = CliJson.SchemaVersion,
                        fields = new[] { "schemaVersion", "command", "data" },
                        description = "Every --json response is this envelope; 'data' is the command-specific payload below.",
                    },
                    commands,
                });
                return 0;
            }

            string version = CliJson.SchemaVersion.ToString(CultureInfo.InvariantCulture);
            Console.Out.WriteLine("Machine-readable output contract (--json):");
            Console.Out.WriteLine();
            Console.Out.WriteLine("  Every --json response is a versioned envelope:");
            Console.Out.WriteLine("    { \"schemaVersion\": " + version + ", \"command\": \"<verb>\", \"data\": { ... } }");
            Console.Out.WriteLine();
            Console.Out.WriteLine("  The 'data' payload per command:");
            Console.Out.WriteLine();

            string[] headers = { "COMMAND", "DATA FIELDS" };
            List<string[]> rows = CommandCatalog.Commands
                .Where(entry => entry.JsonData != null)
                .Select(entry => new[] { entry.Verb, entry.JsonData! })
                .ToList();
            Console.Out.WriteLine(TextTable.Render(headers, rows));
            return 0;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Describe the machine-readable --json envelope and per-command data shapes.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge schema [--json]");
        }
    }
}
