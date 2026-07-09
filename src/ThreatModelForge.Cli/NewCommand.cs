namespace ThreatModelForge.Cli
{
    using System;
    using System.IO;
    using ThreatModelForge.Engine;
    using ThreatModelForge.Formats;
    using ThreatModelForge.Model;

    /// <summary>
    /// Implements the <c>tmforge new</c> command: creates a new threat model, either empty (with a
    /// single diagram) or copied from a <c>--template</c>, and sets its name.
    /// </summary>
    internal static class NewCommand
    {
        /// <summary>
        /// Runs the new command.
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

            CliArgs parsed = CliArgs.Parse(args, new[] { "name", "template", "format" });
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

            string? path = parsed.Positionals.Count > 0 ? parsed.Positionals[0] : null;
            if (string.IsNullOrEmpty(path))
            {
                PrintUsage();
                return 1;
            }

            if (File.Exists(path))
            {
                Console.Error.WriteLine("File already exists: " + path);
                return 1;
            }

            ThreatModelFormatRegistry registry = ThreatModelFormatRegistry.CreateDefault();
            string? formatId = parsed.Get("format");
            IThreatModelFormat? format = !string.IsNullOrEmpty(formatId)
                ? registry.FindById(formatId!)
                : registry.FindByExtension(path!);
            if (format == null)
            {
                Console.Error.WriteLine("Could not determine the output format for '" + path + "'. Use --format <id> (tm7, tmforge-json, drawio, vsdx).");
                return 1;
            }

            if (!format.Capabilities.CanWrite)
            {
                Console.Error.WriteLine("Format '" + format.Id + "' does not support writing.");
                return 1;
            }

            string? template = parsed.Get("template");
            ThreatModel model;
            if (!string.IsNullOrEmpty(template))
            {
                if (!File.Exists(template))
                {
                    Console.Error.WriteLine("Template not found: " + template);
                    return 1;
                }

                model = registry.Load(template!);
            }
            else
            {
                model = new ThreatModel { Version = "1.0" };
                model.DrawingSurfaceList.Add(new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "Diagram 1" });
            }

            string? name = parsed.Get("name");
            string title = !string.IsNullOrWhiteSpace(name) ? name! : Path.GetFileNameWithoutExtension(path!);
            model.MetaInformation ??= new MetaInformation();
            model.MetaInformation.ThreatModelName = title;

            AuthoringSupport.Save(model, path!, format);

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("new", new { path, format = format.Id, name = title });
            }
            else
            {
                Console.Error.WriteLine("Created " + path + " (" + format.Id + ").");
            }

            return 0;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Create a new threat model.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge new [--name <title>] [--template <file>] [--format <id>] [--json] <file>");
        }
    }
}
