namespace ThreatModelForge.Cli
{
    using System;
    using System.IO;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Formats;
    using ThreatModelForge.Model;

    /// <summary>
    /// Implements the <c>tmforge rename</c> command: sets the display name of an element identified
    /// by GUID.
    /// </summary>
    internal static class RenameCommand
    {
        /// <summary>
        /// Runs the rename command.
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

            CliArgs parsed = CliArgs.Parse(args, new[] { "id", "name", "page" });
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

            string? idText = parsed.Get("id");
            string? name = parsed.Get("name");
            if (string.IsNullOrEmpty(idText) || string.IsNullOrEmpty(name))
            {
                Console.Error.WriteLine("--id and --name are required.");
                PrintUsage();
                return 1;
            }

            if (!Guid.TryParse(idText, out Guid id))
            {
                Console.Error.WriteLine("Invalid --id GUID: " + idText);
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

            string? pageSpec = parsed.Get("page");
            DrawingSurfaceModel? diagram;
            if (string.IsNullOrEmpty(pageSpec))
            {
                diagram = AuthoringSupport.FindDiagramContaining(model, id);
            }
            else if (AuthoringSupport.TryResolveDiagram(model, pageSpec!, out DrawingSurfaceModel? resolved, out string? pageError))
            {
                diagram = resolved;
            }
            else
            {
                Console.Error.WriteLine(pageError);
                return 1;
            }

            if (diagram == null || DiagramEditor.FindElement(diagram, id) == null)
            {
                Console.Error.WriteLine("Element not found: " + id);
                return 1;
            }

            DiagramEditor editor = new DiagramEditor(model);
            editor.SetElementName(diagram, id, name!);

            AuthoringSupport.Save(model, input!, format);

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("rename", new { id, name });
            }
            else
            {
                Console.Error.WriteLine("Renamed " + id + " to \"" + name + "\" in " + input + ".");
            }

            return 0;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Rename an element.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge rename --id <guid> --name <name> [--page <name|index>] [--json] <file>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("The element is found on any page by default; --page scopes the search to one page.");
        }
    }
}
