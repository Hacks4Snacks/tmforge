namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Engine;
    using ThreatModelForge.Formats;
    using ThreatModelForge.Model;

    /// <summary>
    /// Implements <c>tmforge merge</c>: a three-way, identity-keyed merge of two edited threat models
    /// against their common ancestor, shaped as a git merge driver (<c>%O %A %B %P</c>). Non-overlapping
    /// edits combine automatically; genuine conflicts are resolved in favor of <c>ours</c>, reported,
    /// and written to a sidecar — the merged <c>.tm7</c> is always valid, never marker-laced.
    /// </summary>
    internal static class MergeCommand
    {
        /// <summary>
        /// Runs the merge command.
        /// </summary>
        /// <param name="args">The command arguments (after the verb): <c>base ours theirs [pathname]</c>.</param>
        /// <returns>Zero on a clean merge; a non-zero value when conflicts remain (or on error).</returns>
        public static int Run(string[] args)
        {
            CliArgs parsed = CliArgs.Parse(args, new[] { "output" });
            if (parsed.Help)
            {
                PrintUsage();
                return 0;
            }

            IReadOnlyList<string> positionals = parsed.Positionals;
            if (positionals.Count < 3)
            {
                PrintUsage();
                return 1;
            }

            string basePath = positionals[0];
            string oursPath = positionals[1];
            string theirsPath = positionals[2];
            string outputPath = parsed.Get("output") ?? oursPath;
            string displayPath = positionals.Count > 3 ? positionals[3] : outputPath;

            foreach (string path in new[] { basePath, oursPath, theirsPath })
            {
                if (!File.Exists(path))
                {
                    Console.Error.WriteLine("File not found: " + path);
                    return 1;
                }
            }

            (ThreatModel baseModel, _) = CliModelLoader.Load(basePath);
            (ThreatModel oursModel, IThreatModelFormat? oursFormat) = CliModelLoader.Load(oursPath);
            (ThreatModel theirsModel, _) = CliModelLoader.Load(theirsPath);

            if (oursFormat == null || !oursFormat.Capabilities.CanWrite)
            {
                Console.Error.WriteLine("The model's format does not support writing.");
                return 1;
            }

            MergeResult result = ModelMerge.Merge(baseModel, oursModel, theirsModel);
            AuthoringSupport.Save(result.Merged, outputPath, oursFormat);

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("merge", BuildPayload(result));
                return result.IsClean ? 0 : 1;
            }

            if (result.IsClean)
            {
                Console.Error.WriteLine("Merged cleanly into " + displayPath + ".");
                return 0;
            }

            string sidecar = displayPath + ".conflicts.json";
            File.WriteAllText(sidecar, CliJson.Serialize(BuildPayload(result)));
            ReportConflicts(result, displayPath, sidecar);
            return 1;
        }

        private static void ReportConflicts(MergeResult result, string displayPath, string sidecar)
        {
            Console.Error.WriteLine(
                result.Conflicts.Count + " conflict(s) merging " + displayPath + "; kept 'ours' where the two sides overlapped:");
            foreach (MergeConflict conflict in result.Conflicts)
            {
                Console.Error.WriteLine("  " + Describe(conflict));
            }

            Console.Error.WriteLine("Review and resolve with 'tmforge set --id <guid> ...'; details in " + sidecar + ".");
        }

        private static string Describe(MergeConflict conflict)
        {
            string head = conflict.ElementKind + " \"" + conflict.Name + "\""
                + (string.IsNullOrEmpty(conflict.DiagramName) ? string.Empty : " [" + conflict.DiagramName + "]");
            switch (conflict.Kind)
            {
                case MergeConflictKind.Property:
                    return head + " " + conflict.Property + ": ours " + Show(conflict.Ours) + " vs theirs " + Show(conflict.Theirs);
                case MergeConflictKind.AddAdd:
                    return head + " added on both sides; " + conflict.Property + ": ours " + Show(conflict.Ours) + " vs theirs " + Show(conflict.Theirs);
                case MergeConflictKind.DeleteModify:
                    return head + " " + conflict.Ours + " here / " + conflict.Theirs + " upstream";
                case MergeConflictKind.DanglingReference:
                    return head + " " + conflict.Property + " points at removed element " + Show(conflict.Ours);
                default:
                    return head;
            }
        }

        private static string Show(string? value)
        {
            return value == null ? "(none)" : "\"" + value + "\"";
        }

        private static object BuildPayload(MergeResult result)
        {
            return new
            {
                status = result.IsClean ? "clean" : "conflict",
                summary = new { conflicts = result.Conflicts.Count },
                conflicts = result.Conflicts.Select(ToPayload).ToArray(),
            };
        }

        private static object ToPayload(MergeConflict conflict)
        {
            return new
            {
                id = conflict.ElementId,
                element = conflict.ElementKind,
                name = conflict.Name,
                diagram = conflict.DiagramName,
                kind = conflict.Kind,
                property = conflict.Property,
                @base = conflict.Base,
                ours = conflict.Ours,
                theirs = conflict.Theirs,
            };
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Three-way merge two edited threat models against their common ancestor.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge merge <base> <ours> <theirs> [<pathname>] [--output <path>] [--json]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Elements are matched by their stable id: non-overlapping edits combine automatically.");
            Console.Error.WriteLine("Conflicts keep 'ours', are reported, and are written to <pathname>.conflicts.json; the");
            Console.Error.WriteLine("merged model is always valid (never marker-laced). Exit code is 0 when clean, 1 on conflict.");
            Console.Error.WriteLine("By default the result is written back to <ours>. Use as a git merge driver with:");
            Console.Error.WriteLine("  .gitattributes:  *.tm7 merge=tmforge");
            Console.Error.WriteLine("  git config merge.tmforge.name \"Threat Model Forge semantic merge\"");
            Console.Error.WriteLine("  git config merge.tmforge.driver \"tmforge merge %O %A %B %P\"");
        }
    }
}
