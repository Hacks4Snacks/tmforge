namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ThreatModelForge.Editing;

    /// <summary>
    /// Implements <c>tmforge stencils</c>: lists the built-in authoring stencil catalog (id, base
    /// DFD primitive, pack, and label), optionally filtered by pack, as an aligned table or JSON.
    /// The ids listed here are the values accepted by <c>tmforge add --stencil</c>.
    /// </summary>
    internal static class StencilsCommand
    {
        /// <summary>
        /// Runs the stencils command.
        /// </summary>
        /// <param name="args">The command arguments (after the verb).</param>
        /// <returns>Zero on success; a non-zero value on error.</returns>
        public static int Run(string[] args)
        {
            CliArgs parsed = CliArgs.Parse(args, new[] { "pack" });
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

            string? pack = parsed.Get("pack");
            IEnumerable<StencilDto> query = StencilCatalog.All;
            if (!string.IsNullOrEmpty(pack))
            {
                query = query.Where(stencil => string.Equals(stencil.Pack, pack, StringComparison.OrdinalIgnoreCase));
            }

            List<StencilDto> stencils = query.ToList();

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("stencils", new { packs = StencilCatalog.Packs, stencils });
                return 0;
            }

            if (stencils.Count == 0)
            {
                Console.Error.WriteLine("No stencils found for pack: " + pack + ".");
                return 1;
            }

            string[] headers = { "ID", "BASE", "PACK", "LABEL" };
            List<string[]> rows = stencils
                .Select(stencil => new[] { stencil.Id, stencil.Base, stencil.Pack, stencil.Label })
                .ToList();

            Console.Out.WriteLine(TextTable.Render(headers, rows));
            return 0;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("List the built-in authoring stencils (use an id with 'tmforge add --stencil <id>').");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge stencils [--pack <id>] [--json]");
        }
    }
}
