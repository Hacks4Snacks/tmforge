namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using ThreatModelForge.Engine;
    using ThreatModelForge.Formats;
    using ThreatModelForge.Model;

    /// <summary>
    /// Implements the <c>tmforge connect</c> command: adds a data-flow connector between two
    /// existing elements, identified by GUID.
    /// </summary>
    internal static class ConnectCommand
    {
        /// <summary>
        /// Runs the connect command.
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

            CliArgs parsed = CliArgs.Parse(args, new[] { "source", "target", "name", "page" }, new[] { "force" });
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

            string? sourceText = parsed.Get("source");
            string? targetText = parsed.Get("target");
            if (string.IsNullOrEmpty(sourceText) || string.IsNullOrEmpty(targetText))
            {
                Console.Error.WriteLine("--source and --target are required.");
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

            ConnectRequest request = new ConnectRequest
            {
                Source = sourceText!,
                Target = targetText!,
                Name = parsed.Get("name"),
                Page = parsed.Get("page"),
                Properties = parsed.Properties,
                Force = parsed.HasFlag("force"),
            };
            if (!AuthoringOperations.Connect(model, request, out Guid id, out Guid source, out Guid target, out IReadOnlyList<string> warnings, out string? error))
            {
                Console.Error.WriteLine(error);
                return 1;
            }

            foreach (string warning in warnings)
            {
                Console.Error.WriteLine(warning);
            }

            AuthoringSupport.Save(model, input!, format);

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("connect", new { id, source, target });
            }
            else
            {
                Console.Error.WriteLine("Connected " + source + " -> " + target + " (" + id + ") in " + input + ".");
            }

            return 0;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Add a data flow between two elements.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge connect --source <ref> --target <ref> [--name <name>] [--page <name|index>] [--property KEY=VALUE]... [--json] <file>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Both endpoints must be on the same page; --page selects it (default: the first page).");
            Console.Error.WriteLine("--source and --target accept a GUID, an element --alias, or a unique element name.");
            Console.Error.WriteLine("Set flow properties the analyzer checks, e.g. --property Protocol=HTTPS --property Port=443 --property DataType=\"Customer Content\".");
            Console.Error.WriteLine("Mark a non-network flow to skip protocol/port/cleartext checks: --property Channel=In-Process|Local-file|Unix-socket|Loopback.");
            Console.Error.WriteLine("List every property and its allowed values with 'tmforge properties --base flow'.");
            Console.Error.WriteLine("Values are validated against the schema; pass --force to store an unknown property or value.");
        }
    }
}
