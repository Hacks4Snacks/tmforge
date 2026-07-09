namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Engine;
    using ThreatModelForge.Formats;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Implements the <c>tmforge add</c> command: adds a process, data store, external interactor,
    /// or trust boundary to a model's first diagram, placing it deterministically when no
    /// coordinates are given.
    /// </summary>
    internal static class AddCommand
    {
        /// <summary>
        /// Runs the add command.
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

            CliArgs parsed = CliArgs.Parse(args, new[] { "name", "left", "top", "stencil", "width", "height", "page", "alias", "boundary" }, new[] { "force" });
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

            string? stencilId = parsed.Get("stencil");
            string? kindNoun = null;
            string input;

            if (!string.IsNullOrEmpty(stencilId))
            {
                if (parsed.Positionals.Count > 1)
                {
                    Console.Error.WriteLine("Specify either an element kind or --stencil, not both.");
                    return 1;
                }

                if (parsed.Positionals.Count < 1)
                {
                    PrintUsage();
                    return 1;
                }

                input = parsed.Positionals[0];
            }
            else
            {
                if (parsed.Positionals.Count < 2)
                {
                    PrintUsage();
                    return 1;
                }

                kindNoun = parsed.Positionals[0];
                input = parsed.Positionals[1];
            }

            if (!AuthoringSupport.TryResolveKind(kindNoun, stencilId, out StencilKind kind, out StencilDto? stencil, out string? kindError))
            {
                Console.Error.WriteLine(kindError);
                return 1;
            }

            if (!File.Exists(input))
            {
                Console.Error.WriteLine("File not found: " + input);
                return 1;
            }

            (ThreatModel model, IThreatModelFormat? format) = CliModelLoader.Load(input);
            if (format == null || !format.Capabilities.CanWrite)
            {
                Console.Error.WriteLine("The model's format does not support writing.");
                return 1;
            }

            AddRequest request = new AddRequest
            {
                Kind = kind,
                Stencil = stencil,
                Name = parsed.Get("name"),
                Page = parsed.Get("page"),
                Left = TryGetInt(parsed, "left", out int parsedLeft) ? parsedLeft : null,
                Top = TryGetInt(parsed, "top", out int parsedTop) ? parsedTop : null,
                Width = TryGetInt(parsed, "width", out int parsedWidth) ? parsedWidth : null,
                Height = TryGetInt(parsed, "height", out int parsedHeight) ? parsedHeight : null,
                Alias = parsed.Get("alias"),
                Boundary = parsed.Get("boundary"),
                Properties = parsed.Properties,
                Force = parsed.HasFlag("force"),
            };
            if (!AuthoringOperations.Add(model, request, out Guid id, out IReadOnlyList<string> warnings, out string? error))
            {
                Console.Error.WriteLine(error);
                return 1;
            }

            foreach (string warning in warnings)
            {
                Console.Error.WriteLine(warning);
            }

            AuthoringSupport.Save(model, input, format);

            DrawingSurfaceModel? placedDiagram = AuthoringSupport.FindDiagramContaining(model, id);
            Entity? added = placedDiagram != null ? DiagramEditor.FindElement(placedDiagram, id) : null;
            string effectiveName = added != null ? DiagramElementHelper.GetName(added) : string.Empty;
            string? alias = parsed.Get("alias");

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("add", new { id, kind, name = effectiveName, stencil = stencil?.Id, diagramId = placedDiagram?.Guid, alias });
            }
            else
            {
                Console.Error.WriteLine("Added " + (stencil != null ? stencil.Id : kind.ToString()) + " " + id + " to " + input + ".");
            }

            return 0;
        }

        private static bool TryGetInt(CliArgs parsed, string name, out int value)
        {
            string? raw = parsed.Get(name);
            if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            value = 0;
            return false;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Add an element to a threat model.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge add <process|store|external|boundary> [options] <file>");
            Console.Error.WriteLine("  tmforge add --stencil <id> [options] <file>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  --name <name>             Element name.");
            Console.Error.WriteLine("  --alias <name>            Stable handle (resolvable by connect/set/remove/rename/show) with a deterministic, citeable id.");
            Console.Error.WriteLine("  --boundary <ref>          Place the element inside this trust boundary (by alias/name/GUID) and record membership.");
            Console.Error.WriteLine("  --left <n> --top <n>      Position (default: auto-placed on a grid).");
            Console.Error.WriteLine("  --width <n> --height <n>   Size; boundaries default to 260x180 so they enclose.");
            Console.Error.WriteLine("  --page <name|index>       Target page (default: the first page; one is created if none exists).");
            Console.Error.WriteLine("  --property KEY=VALUE      Set a custom property (repeatable), e.g. --property AuthenticationScheme=OAuth.");
            Console.Error.WriteLine("  --force                   Store unknown/invalid property values instead of rejecting them.");
            Console.Error.WriteLine("  --json                    Machine-readable output.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Run 'tmforge stencils' to list available stencils (for example, azure-sql, azure-key-vault).");
        }
    }
}
