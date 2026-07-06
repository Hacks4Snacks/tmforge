namespace ThreatModelForge.Cli
{
    using System;
    using System.IO;
    using System.Text;
    using ThreatModelForge.Formats;
    using ThreatModelForge.Model;
    using ThreatModelForge.Reporting;

    /// <summary>
    /// Implements the <c>tmforge report</c> command.
    /// </summary>
    internal static class ReportCommand
    {
        /// <summary>
        /// Runs the report command.
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

            ThreatModel model = ThreatModelFormatRegistry.CreateDefault().Load(input!);

            string html = new HtmlReportWriter().Write(model);

            if (parsed.Json)
            {
                if (!string.IsNullOrEmpty(output))
                {
                    File.WriteAllText(output!, html);
                }

                CliJson.WriteEnvelope(
                    "report",
                    new
                    {
                        output = string.IsNullOrEmpty(output) ? null : output,
                        format = "html",
                        bytes = Encoding.UTF8.GetByteCount(html),
                    });
                return 0;
            }

            if (string.IsNullOrEmpty(output))
            {
                Console.Out.Write(html);
            }
            else
            {
                File.WriteAllText(output!, html);
                Console.Error.WriteLine("Report written to " + output);
            }

            return 0;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Threat Model Forge report generator.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge report [--out <path.html>] [--json] <model.tm7>");
            Console.Error.WriteLine("If --out is omitted, the report is written to standard output.");
        }
    }
}
