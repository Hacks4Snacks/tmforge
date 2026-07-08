namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Formats;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Implements <c>tmforge layout</c>: applies a deterministic, dependency-free layered auto-layout
    /// to a model's pages, so an author never has to hand-place coordinates. Components are arranged
    /// left-to-right by their data flows and connectors are re-routed; trust boundaries are left where
    /// they are (so this arranges the data-flow graph rather than preserving boundary placement).
    /// </summary>
    internal static class LayoutCommand
    {
        /// <summary>
        /// Runs the layout command.
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

            CliArgs parsed = CliArgs.Parse(args, new[] { "node-spacing", "layer-spacing", "page" });
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

            (ThreatModel model, IThreatModelFormat? format) = CliModelLoader.Load(input!);
            if (format == null || !format.Capabilities.CanWrite)
            {
                Console.Error.WriteLine("The model's format does not support writing.");
                return 1;
            }

            LayoutOptions options = new LayoutOptions();
            if (TryGetInt(parsed, "node-spacing", out int nodeSpacing))
            {
                options.NodeSpacing = nodeSpacing;
            }

            if (TryGetInt(parsed, "layer-spacing", out int layerSpacing))
            {
                options.LayerSpacing = layerSpacing;
            }

            string? pageSpec = parsed.Get("page");
            List<DrawingSurfaceModel> targets = new List<DrawingSurfaceModel>();
            if (string.IsNullOrEmpty(pageSpec))
            {
                targets.AddRange(model.DrawingSurfaceList);
            }
            else if (AuthoringSupport.TryResolveDiagram(model, pageSpec!, out DrawingSurfaceModel? resolved, out string? pageError))
            {
                targets.Add(resolved!);
            }
            else
            {
                Console.Error.WriteLine(pageError);
                return 1;
            }

            int components = 0;
            foreach (DrawingSurfaceModel diagram in targets)
            {
                components += diagram.Borders.Values.OfType<DrawingElement>().Count(element => !(element is BorderBoundary));
                DiagramLayout.Apply(diagram, options);
            }

            AuthoringSupport.Save(model, input!, format);

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("layout", new { pages = targets.Count, components });
            }
            else
            {
                Console.Error.WriteLine("Laid out " + components + " component(s) across " + targets.Count + " page(s) in " + input + ".");
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
            Console.Error.WriteLine("Auto-lay-out a model's pages (layered left-to-right; boundaries are left in place).");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge layout [--page <name|index>] [--node-spacing <n>] [--layer-spacing <n>] [--json] <model>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Arranges components by their data flows so you need not hand-place coordinates.");
        }
    }
}
