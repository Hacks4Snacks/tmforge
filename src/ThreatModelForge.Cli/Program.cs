namespace ThreatModelForge.Cli
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Text.Json;
    using System.Xml;

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
                try
                {
                    return command.Run(rest);
                }
                catch (Exception ex) when (IsInputError(ex))
                {
                    Console.Error.WriteLine(ex.Message);
                    return 1;
                }
            }

            Console.Error.WriteLine("Unknown command: " + verb);
            PrintUsage();
            return 1;
        }

        /// <summary>
        /// Reports whether a failure was caused by what the user pointed the tool at rather than by a
        /// defect in the tool. These become a one-line message and exit 1; everything else is left to
        /// crash, because an unexpected failure should stay loud and keep its stack trace.
        /// <para>
        /// Classification mirrors the API's request-error handler, extended with the file-system and
        /// document-parsing failures a command-line tool meets that an HTTP body cannot produce.
        /// </para>
        /// </summary>
        /// <param name="exception">The unhandled exception.</param>
        /// <returns><see langword="true"/> when the user's input caused it.</returns>
        internal static bool IsInputError(Exception exception)
        {
            switch (exception)
            {
                // A null argument is this tool calling itself wrongly, not the user's doing. Checked
                // first because it derives from ArgumentException, which is accepted below.
                case ArgumentNullException:
                    return false;

                // No registered format can read the named file, or an unknown format id was requested.
                case NotSupportedException:

                // A missing or empty required value that reached the engine.
                case ArgumentException:

                // A missing file or directory, an unreadable path, a corrupt container. InvalidDataException
                // is listed separately because it derives from SystemException, not IOException.
                case IOException:
                case InvalidDataException:
                case UnauthorizedAccessException:

                // The named file is not the document it claims to be.
                case JsonException:
                case XmlException:
                case SerializationException:
                case FormatException:
                    return true;
                default:
                    return false;
            }
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
