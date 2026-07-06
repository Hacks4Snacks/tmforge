namespace ThreatModelForge.Cli
{
    using System;
    using System.Globalization;
    using System.IO;
    using ThreatModelForge.Model;

    /// <summary>
    /// Implements the <c>tmforge render</c> command: draws a model's diagrams to the terminal with
    /// Unicode box-drawing and ANSI color. <c>--plain</c> emits ASCII without color for pipes and
    /// logs; color is disabled automatically when output is redirected or <c>NO_COLOR</c> is set.
    /// </summary>
    internal static class RenderCommand
    {
        /// <summary>
        /// Runs the render command.
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

            CliArgs parsed = CliArgs.Parse(args, new[] { "width", "height" }, new[] { "plain" });
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

            bool plain = parsed.HasFlag("plain");
            bool unicode = !plain;
            bool color = !plain
                && !Console.IsOutputRedirected
                && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
            int width = ParseDimension(parsed.Get("width"), 100, 20, 400);
            int height = ParseDimension(parsed.Get("height"), 30, 10, 200);

            Console.Out.Write(TerminalRenderer.Render(model, width, height, unicode, color));
            return 0;
        }

        private static int ParseDimension(string? raw, int fallback, int minimum, int maximum)
        {
            if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return Math.Clamp(value, minimum, maximum);
            }

            return fallback;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Render a threat model's diagrams in the terminal.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge render [--plain] [--width <n>] [--height <n>] <file>");
            Console.Error.WriteLine("--plain emits ASCII without color, for piping and logs.");
        }
    }
}
