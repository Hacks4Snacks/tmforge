namespace ThreatModelForge.Cli
{
    using System;
    using System.Reflection;

    /// <summary>
    /// The <c>tmforge</c> command-line tool. Dispatches to a verb-specific command.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The entry point. The first argument selects the verb; the rest are passed through.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <returns>Zero on success; a non-zero value on error.</returns>
        public static int Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            string verb = args[0];
            string[] rest = args[1..];

            switch (verb)
            {
                case "-?":
                case "-h":
                case "--help":
                    PrintUsage();
                    return 0;

                case "--version":
                    Console.Out.WriteLine(GetVersion());
                    return 0;
            }

            CommandInfo? command = CommandCatalog.Find(verb);
            if (command != null)
            {
                return command.Run(rest);
            }

            Console.Error.WriteLine("Unknown command: " + verb);
            PrintUsage();
            return 1;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Threat Model Forge.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge <command> [options]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Commands:");

            int width = 0;
            foreach (CommandInfo command in CommandCatalog.Commands.Where(command => command.Verb.Length > width))
            {
                width = command.Verb.Length;
            }

            foreach (CommandInfo command in CommandCatalog.Commands)
            {
                Console.Error.WriteLine("  " + command.Verb.PadRight(width) + "  " + command.Summary);
            }

            Console.Error.WriteLine();
            Console.Error.WriteLine("Add --json for machine-readable output. Options accept --name value or --name=value.");
            Console.Error.WriteLine("Run 'tmforge <command> --help' for command-specific options.");
        }

        private static string GetVersion()
        {
            return typeof(Program)
                .Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? string.Empty;
        }
    }
}
