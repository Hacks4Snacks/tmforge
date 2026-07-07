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
                case "open":
                    return OpenCommand.Run(rest);

                case "list":
                    return ListCommand.Run(rest);

                case "show":
                    return ShowCommand.Run(rest);

                case "stencils":
                    return StencilsCommand.Run(rest);

                case "properties":
                    return PropertiesCommand.Run(rest);

                case "render":
                    return RenderCommand.Run(rest);

                case "new":
                    return NewCommand.Run(rest);

                case "add":
                    return AddCommand.Run(rest);

                case "connect":
                    return ConnectCommand.Run(rest);

                case "remove":
                    return RemoveCommand.Run(rest);

                case "rename":
                    return RenameCommand.Run(rest);

                case "set":
                    return SetCommand.Run(rest);

                case "page":
                    return PageCommand.Run(rest);

                case "lint":
                    return LintCommand.Run(rest);

                case "report":
                    return ReportCommand.Run(rest);

                case "convert":
                    return ConvertCommand.Run(rest);

                case "-?":
                case "-h":
                case "--help":
                    PrintUsage();
                    return 0;

                case "--version":
                    Console.Out.WriteLine(GetVersion());
                    return 0;

                default:
                    Console.Error.WriteLine("Unknown command: " + verb);
                    PrintUsage();
                    return 1;
            }
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Threat Model Forge.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge <command> [options]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Commands:");
            Console.Error.WriteLine("  open      Summarize a threat model (counts of elements, flows, and threats).");
            Console.Error.WriteLine("  list      List components, flows, boundaries, threats, or diagrams.");
            Console.Error.WriteLine("  show      Show an element/flow's name, type, and custom properties.");
            Console.Error.WriteLine("  stencils  List the built-in authoring stencils (ids for 'add --stencil').");
            Console.Error.WriteLine("  properties  List the typed property schema (custom properties the linter reads).");
            Console.Error.WriteLine("  render    Render the diagram in the terminal (Unicode/ANSI; --plain for ASCII).");
            Console.Error.WriteLine("  new       Create a new threat model.");
            Console.Error.WriteLine("  add       Add a process, store, external interactor, or boundary.");
            Console.Error.WriteLine("  connect   Add a data flow between two elements.");
            Console.Error.WriteLine("  remove    Remove an element (and its connected flows).");
            Console.Error.WriteLine("  rename    Rename an element.");
            Console.Error.WriteLine("  set       Set an element/flow's name or properties (protocol, port, auth, ...).");
            Console.Error.WriteLine("  page      List, add, rename, reorder, or remove pages (diagrams).");
            Console.Error.WriteLine("  lint      Validate a threat model against a rule set.");
            Console.Error.WriteLine("  report    Generate an HTML report from a threat model.");
            Console.Error.WriteLine("  convert   Convert a threat model between file formats.");
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
