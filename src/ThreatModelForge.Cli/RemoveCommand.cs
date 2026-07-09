namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using ThreatModelForge.Engine;
    using ThreatModelForge.Formats;
    using ThreatModelForge.Model;

    /// <summary>
    /// Implements the <c>tmforge remove</c> command: removes an element by GUID. Removing a
    /// component also removes any data flows attached to it.
    /// </summary>
    internal static class RemoveCommand
    {
        /// <summary>
        /// Runs the remove command.
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

            CliArgs parsed = CliArgs.Parse(args, new[] { "id", "page" });
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

            RemoveRequest request = new RemoveRequest { Id = idText!, Page = parsed.Get("page") };
            if (!AuthoringOperations.Remove(model, request, out IReadOnlyList<Guid> removed, out string? error))
            {
                Console.Error.WriteLine(error);
                return 1;
            }

            AuthoringSupport.Save(model, input!, format);

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("remove", new { removed });
            }
            else
            {
                Console.Error.WriteLine("Removed " + removed.Count + " element(s) from " + input + ".");
            }

            return 0;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Remove an element (and its connected flows).");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge remove --id <ref> [--page <name|index>] [--json] <file>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("--id accepts a GUID, an element --alias, or a unique element name.");
            Console.Error.WriteLine("The element is found on any page by default; --page scopes the search to one page.");
        }
    }
}
