namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Implements <c>tmforge show</c>: a read-only view of an element or flow's name, type, stencil,
    /// and custom properties. This is the verify counterpart of <c>tmforge set</c> — an agent can
    /// confirm what it wrote without re-running <c>lint</c>.
    /// </summary>
    internal static class ShowCommand
    {
        /// <summary>
        /// Runs the show command.
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

            CliArgs parsed = CliArgs.Parse(args, new[] { "id" });
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
            if (string.IsNullOrEmpty(idText))
            {
                Console.Error.WriteLine("--id is required.");
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

            (ThreatModel model, _) = CliModelLoader.Load(input!);

            Entity? target = null;
            bool isFlow = false;
            foreach (DrawingSurfaceModel diagram in model.DrawingSurfaceList)
            {
                Entity? found = DiagramEditor.FindElement(diagram, id);
                if (found != null)
                {
                    target = found;
                    isFlow = diagram.Lines.ContainsKey(id);
                    break;
                }
            }

            if (target == null)
            {
                Console.Error.WriteLine("Element not found: " + id);
                return 1;
            }

            string name = DiagramElementHelper.GetName(target);
            IReadOnlyDictionary<string, string> properties = DiagramElementHelper.GetCustomProperties(target);
            string kind = isFlow ? "flow" : "element";
            string? stencil = properties.TryGetValue("StencilType", out string? stencilType) ? stencilType : null;
            string? stencilLabel = stencil != null ? StencilCatalog.Find(stencil)?.Label : null;

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("show", new { id, kind, name, stencil, stencilLabel, properties });
                return 0;
            }

            Console.WriteLine("id:      " + id);
            Console.WriteLine("kind:    " + kind);
            Console.WriteLine("name:    " + name);
            if (stencilLabel != null)
            {
                Console.WriteLine("stencil: " + stencilLabel + " (" + stencil + ")");
            }

            foreach (KeyValuePair<string, string> pair in properties)
            {
                if (string.Equals(pair.Key, "StencilType", StringComparison.Ordinal))
                {
                    continue;
                }

                Console.WriteLine("  " + pair.Key + " = " + pair.Value);
            }

            return 0;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Show an element or flow's name, type, and custom properties.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge show --id <guid> [--json] <file>");
        }
    }
}
