namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using ThreatModelForge.Editing;
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

            CliArgs parsed = CliArgs.Parse(args, new[] { "name", "left", "top", "stencil", "width", "height" }, new[] { "force" });
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
            StencilDto? stencil = null;
            StencilKind kind;
            string input;

            if (!string.IsNullOrEmpty(stencilId))
            {
                stencil = StencilCatalog.Find(stencilId!);
                if (stencil == null)
                {
                    Console.Error.WriteLine("Unknown stencil: " + stencilId + " (run 'tmforge stencils' to list available stencils).");
                    return 1;
                }

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

                if (!AuthoringSupport.TryParseKind(stencil.Base, out kind))
                {
                    Console.Error.WriteLine("Stencil '" + stencil.Id + "' has an unrecognized base primitive: " + stencil.Base + ".");
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

                string kindText = parsed.Positionals[0];
                input = parsed.Positionals[1];

                if (!AuthoringSupport.TryParseKind(kindText, out kind))
                {
                    Console.Error.WriteLine("Unknown element kind: " + kindText + " (expected process, store, external, or boundary).");
                    return 1;
                }
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

            DrawingSurfaceModel diagram = AuthoringSupport.GetOrCreateFirstDiagram(model);
            DiagramEditor editor = new DiagramEditor(model);

            (int defaultLeft, int defaultTop) = AuthoringSupport.NextPosition(diagram);
            int left = TryGetInt(parsed, "left", out int parsedLeft) ? parsedLeft : defaultLeft;
            int top = TryGetInt(parsed, "top", out int parsedTop) ? parsedTop : defaultTop;
            bool hasWidth = TryGetInt(parsed, "width", out int argWidth);
            bool hasHeight = TryGetInt(parsed, "height", out int argHeight);

            Guid id = editor.AddElement(diagram, kind, left, top);
            if (kind == StencilKind.TrustBoundary)
            {
                editor.ResizeElement(diagram, id, left, top, hasWidth ? argWidth : 260, hasHeight ? argHeight : 180);
            }
            else if (hasWidth || hasHeight)
            {
                DrawingElement? placed = DiagramEditor.FindElement(diagram, id) as DrawingElement;
                editor.ResizeElement(diagram, id, left, top, hasWidth ? argWidth : placed?.Width ?? 100, hasHeight ? argHeight : placed?.Height ?? 60);
            }

            string? name = parsed.Get("name");
            if (string.IsNullOrEmpty(name) && stencil != null)
            {
                name = stencil.Label;
            }

            if (!string.IsNullOrEmpty(name))
            {
                editor.SetElementName(diagram, id, name!);
            }

            Entity? added = DiagramEditor.FindElement(diagram, id);
            if (stencil != null && added != null)
            {
                DiagramElementHelper.SetCustomProperty(added, "StencilType", stencil.Id);
                foreach (KeyValuePair<string, string> preset in stencil.Defaults)
                {
                    DiagramElementHelper.SetCustomProperty(added, preset.Key, preset.Value);
                }
            }

            if (added != null && parsed.Properties.Count > 0)
            {
                if (!AuthoringSupport.TryApplyProperties(added, parsed.Properties, AuthoringSupport.SchemaBase(kind), parsed.HasFlag("force"), out string? propertyError, out IReadOnlyList<string> propertyWarnings))
                {
                    Console.Error.WriteLine(propertyError);
                    return 1;
                }

                foreach (string warning in propertyWarnings)
                {
                    Console.Error.WriteLine(warning);
                }
            }

            string effectiveName = added != null ? DiagramElementHelper.GetName(added) : string.Empty;

            AuthoringSupport.Save(model, input, format);

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("add", new { id, kind, name = effectiveName, stencil = stencil?.Id, diagramId = diagram.Guid });
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
            Console.Error.WriteLine("  --left <n> --top <n>      Position (default: auto-placed on a grid).");
            Console.Error.WriteLine("  --width <n> --height <n>   Size; boundaries default to 260x180 so they enclose.");
            Console.Error.WriteLine("  --property KEY=VALUE      Set a custom property (repeatable), e.g. --property AuthenticationScheme=OAuth.");
            Console.Error.WriteLine("  --force                   Store unknown/invalid property values instead of rejecting them.");
            Console.Error.WriteLine("  --json                    Machine-readable output.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Run 'tmforge stencils' to list available stencils (for example, azure-sql, azure-key-vault).");
        }
    }
}
