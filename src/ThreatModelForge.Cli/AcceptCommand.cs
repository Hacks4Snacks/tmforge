namespace ThreatModelForge.Cli
{
    using System;
    using System.IO;
    using ThreatModelForge.Analysis;
    using ThreatModelForge.Engine;
    using ThreatModelForge.Formats;
    using ThreatModelForge.Model;

    /// <summary>
    /// Implements <c>tmforge accept</c>: records inline risk acceptance for a generated threat.
    /// Acceptance is a threat state — the threat is moved to <c>NotApplicable</c> with a justification
    /// and stays visible in the register and report, no longer counted as open. Because it is scoped to
    /// one threat (a single pattern on a single interaction), it can never silently swallow an
    /// unrelated finding.
    /// </summary>
    internal static class AcceptCommand
    {
        /// <summary>
        /// Runs the accept command.
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

            CliArgs parsed = CliArgs.Parse(args, new[] { "threat", "reason" }, Array.Empty<string>());
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
            string? threatId = parsed.Get("threat");
            string? reason = parsed.Get("reason");
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(threatId))
            {
                PrintUsage();
                return 1;
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                Console.Error.WriteLine("--reason is required: record why the risk is accepted.");
                PrintUsage();
                return 1;
            }

            if (!File.Exists(input))
            {
                Console.Error.WriteLine("File not found: " + input);
                return 1;
            }

            (ThreatModel model, IThreatModelFormat? format) = CliModelLoader.Load(input!);
            if (format == null || !format.Capabilities.CanWrite)
            {
                Console.Error.WriteLine("The model's format does not support writing.");
                return 1;
            }

            if (!ThreatGenerator.Accept(model, threatId!, reason!))
            {
                Console.Error.WriteLine("Threat not found: " + threatId + ". Persist threats first with 'tmforge threats --write', then use 'tmforge list threats' to find its id.");
                return 1;
            }

            AuthoringSupport.Save(model, input!, format);

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("accept", new
                {
                    threat = threatId,
                    state = "NotApplicable",
                    reason,
                });
            }
            else
            {
                Console.Error.WriteLine("Accepted " + threatId + " in " + input + ": " + reason);
            }

            return 0;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Accept the risk of a generated threat (marks it not-applicable with a justification).");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge accept --threat <id> --reason <text> [--json] <file>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("--threat accepts the threat's register key, interaction key, or numeric id (from 'tmforge list threats').");
        }
    }
}
