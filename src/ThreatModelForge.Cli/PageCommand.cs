namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using ThreatModelForge.Analysis;
    using ThreatModelForge.Engine;
    using ThreatModelForge.Formats;
    using ThreatModelForge.Model;

    /// <summary>
    /// Implements the <c>tmforge page</c> command: lists, adds, renames, reorders, and removes the
    /// pages (diagrams) of a threat model, mirroring the Studio's page tab strip.
    /// </summary>
    internal static class PageCommand
    {
        /// <summary>
        /// Runs the page command by dispatching to a subcommand.
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

            string sub = args[0];
            string[] rest = args[1..];
            switch (sub.ToLowerInvariant())
            {
                case "ls":
                case "list":
                    return List(rest);
                case "add":
                    return Add(rest);
                case "rename":
                    return Rename(rest);
                case "rm":
                case "remove":
                case "delete":
                    return Remove(rest);
                case "reorder":
                case "move":
                    return Reorder(rest);
                case "-?":
                case "-h":
                case "--help":
                    PrintUsage();
                    return 0;
                default:
                    Console.Error.WriteLine("Unknown page subcommand: " + sub);
                    PrintUsage();
                    return 1;
            }
        }

        private static int List(string[] args)
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

            (ThreatModel model, IThreatModelFormat? format) = CliModelLoader.Load(input!);
            if (format == null)
            {
                Console.Error.WriteLine("Unrecognized model format: " + input);
                return 1;
            }

            List<DiagramSummary> summaries = model.DrawingSurfaceList.Select(DiagramSummary.FromDrawingSurfaceModel).ToList();
            if (parsed.Json)
            {
                var items = summaries.Select((summary, i) => new
                {
                    index = i + 1,
                    name = summary.Header ?? string.Empty,
                    components = summary.ComponentCount,
                    flows = summary.ConnectorCount,
                    boundaries = summary.TrustBoundaryCount,
                    id = summary.ID,
                }).ToList();
                CliJson.WriteEnvelope("page ls", new { count = items.Count, items });
                return 0;
            }

            if (summaries.Count == 0)
            {
                Console.WriteLine("No pages found.");
                return 0;
            }

            string[] headers = { "#", "NAME", "COMPONENTS", "FLOWS", "BOUNDARIES", "ID" };
            List<string[]> rows = new List<string[]>();
            int index = 1;
            foreach (DiagramSummary summary in summaries)
            {
                rows.Add(new[]
                {
                    index.ToString(CultureInfo.InvariantCulture),
                    summary.Header ?? string.Empty,
                    summary.ComponentCount.ToString(CultureInfo.InvariantCulture),
                    summary.ConnectorCount.ToString(CultureInfo.InvariantCulture),
                    summary.TrustBoundaryCount.ToString(CultureInfo.InvariantCulture),
                    summary.ID.ToString(),
                });
                index++;
            }

            Console.WriteLine(TextTable.Render(headers, rows));
            return 0;
        }

        private static int Add(string[] args)
        {
            CliArgs parsed = CliArgs.Parse(args, new[] { "name" });
            if (parsed.Help)
            {
                PrintUsage();
                return 0;
            }

            if (parsed.UnknownFlags.Count > 0)
            {
                Console.Error.WriteLine("Unknown option: " + parsed.UnknownFlags[0]);
                return 1;
            }

            if (!TryLoadWritable(parsed, out string input, out ThreatModel model, out IThreatModelFormat format))
            {
                return 1;
            }

            string name = parsed.Get("name") ?? ("Diagram " + (model.DrawingSurfaceList.Count + 1).ToString(CultureInfo.InvariantCulture));
            DrawingSurfaceModel page = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = name };
            model.DrawingSurfaceList.Add(page);
            AuthoringSupport.Save(model, input, format);

            int position = model.DrawingSurfaceList.Count;
            if (parsed.Json)
            {
                CliJson.WriteEnvelope("page add", new { index = position, name, id = page.Guid });
            }
            else
            {
                Console.Error.WriteLine("Added page \"" + name + "\" (page " + position + ") to " + input + ".");
            }

            return 0;
        }

        private static int Rename(string[] args)
        {
            CliArgs parsed = CliArgs.Parse(args, new[] { "page", "name" });
            if (parsed.Help)
            {
                PrintUsage();
                return 0;
            }

            if (parsed.UnknownFlags.Count > 0)
            {
                Console.Error.WriteLine("Unknown option: " + parsed.UnknownFlags[0]);
                return 1;
            }

            string? pageSpec = parsed.Get("page");
            string? name = parsed.Get("name");
            if (string.IsNullOrEmpty(pageSpec) || string.IsNullOrEmpty(name))
            {
                Console.Error.WriteLine("--page and --name are required.");
                PrintUsage();
                return 1;
            }

            if (!TryLoadWritable(parsed, out string input, out ThreatModel model, out IThreatModelFormat format))
            {
                return 1;
            }

            if (!AuthoringSupport.TryResolveDiagram(model, pageSpec!, out DrawingSurfaceModel? page, out string? error))
            {
                Console.Error.WriteLine(error);
                return 1;
            }

            page!.Header = name;
            AuthoringSupport.Save(model, input, format);

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("page rename", new { id = page.Guid, name });
            }
            else
            {
                Console.Error.WriteLine("Renamed page to \"" + name + "\" in " + input + ".");
            }

            return 0;
        }

        private static int Remove(string[] args)
        {
            CliArgs parsed = CliArgs.Parse(args, new[] { "page" });
            if (parsed.Help)
            {
                PrintUsage();
                return 0;
            }

            if (parsed.UnknownFlags.Count > 0)
            {
                Console.Error.WriteLine("Unknown option: " + parsed.UnknownFlags[0]);
                return 1;
            }

            string? pageSpec = parsed.Get("page");
            if (string.IsNullOrEmpty(pageSpec))
            {
                Console.Error.WriteLine("--page is required.");
                PrintUsage();
                return 1;
            }

            if (!TryLoadWritable(parsed, out string input, out ThreatModel model, out IThreatModelFormat format))
            {
                return 1;
            }

            if (model.DrawingSurfaceList.Count <= 1)
            {
                Console.Error.WriteLine("Cannot delete the last page; a model must have at least one page.");
                return 1;
            }

            if (!AuthoringSupport.TryResolveDiagram(model, pageSpec!, out DrawingSurfaceModel? page, out string? error))
            {
                Console.Error.WriteLine(error);
                return 1;
            }

            string removedName = page!.Header ?? string.Empty;
            model.DrawingSurfaceList.Remove(page);
            AuthoringSupport.Save(model, input, format);

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("page rm", new { id = page.Guid, name = removedName, remaining = model.DrawingSurfaceList.Count });
            }
            else
            {
                Console.Error.WriteLine("Removed page \"" + removedName + "\" from " + input + ".");
            }

            return 0;
        }

        private static int Reorder(string[] args)
        {
            CliArgs parsed = CliArgs.Parse(args, new[] { "page", "to" });
            if (parsed.Help)
            {
                PrintUsage();
                return 0;
            }

            if (parsed.UnknownFlags.Count > 0)
            {
                Console.Error.WriteLine("Unknown option: " + parsed.UnknownFlags[0]);
                return 1;
            }

            string? pageSpec = parsed.Get("page");
            string? toText = parsed.Get("to");
            if (string.IsNullOrEmpty(pageSpec) || string.IsNullOrEmpty(toText))
            {
                Console.Error.WriteLine("--page and --to are required.");
                PrintUsage();
                return 1;
            }

            if (!int.TryParse(toText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int to))
            {
                Console.Error.WriteLine("Invalid --to index: " + toText);
                return 1;
            }

            if (!TryLoadWritable(parsed, out string input, out ThreatModel model, out IThreatModelFormat format))
            {
                return 1;
            }

            if (!AuthoringSupport.TryResolveDiagram(model, pageSpec!, out DrawingSurfaceModel? page, out string? error))
            {
                Console.Error.WriteLine(error);
                return 1;
            }

            int count = model.DrawingSurfaceList.Count;
            if (to < 1 || to > count)
            {
                Console.Error.WriteLine("Target index " + to + " is out of range (1.." + count + ").");
                return 1;
            }

            model.DrawingSurfaceList.Remove(page!);
            model.DrawingSurfaceList.Insert(to - 1, page!);
            AuthoringSupport.Save(model, input, format);

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("page reorder", new { id = page!.Guid, name = page.Header ?? string.Empty, index = to });
            }
            else
            {
                Console.Error.WriteLine("Moved page \"" + (page!.Header ?? string.Empty) + "\" to position " + to + " in " + input + ".");
            }

            return 0;
        }

        private static bool TryLoadWritable(CliArgs parsed, out string input, out ThreatModel model, out IThreatModelFormat format)
        {
            input = string.Empty;
            model = null!;
            format = null!;

            string? path = parsed.Positionals.Count > 0 ? parsed.Positionals[0] : null;
            if (string.IsNullOrEmpty(path))
            {
                PrintUsage();
                return false;
            }

            if (!File.Exists(path))
            {
                Console.Error.WriteLine("File not found: " + path);
                return false;
            }

            (ThreatModel loaded, IThreatModelFormat? loadedFormat) = CliModelLoader.Load(path!);
            if (loadedFormat == null || !loadedFormat.Capabilities.CanWrite)
            {
                Console.Error.WriteLine("The model's format does not support writing.");
                return false;
            }

            input = path!;
            model = loaded;
            format = loadedFormat;
            return true;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Manage the pages (diagrams) of a threat model.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge page ls [--json] <file>");
            Console.Error.WriteLine("  tmforge page add [--name <name>] [--json] <file>");
            Console.Error.WriteLine("  tmforge page rename --page <name|index> --name <newname> [--json] <file>");
            Console.Error.WriteLine("  tmforge page rm --page <name|index> [--json] <file>");
            Console.Error.WriteLine("  tmforge page reorder --page <name|index> --to <index> [--json] <file>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("--page accepts a 1-based index or a page name; indices and --to are 1-based.");
            Console.Error.WriteLine("The last page cannot be removed. Author onto a page with 'tmforge add --page <name|index>'.");
        }
    }
}
