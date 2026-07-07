namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Formats;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

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

            if (!Guid.TryParse(sourceText, out Guid source))
            {
                Console.Error.WriteLine("Invalid --source GUID: " + sourceText);
                return 1;
            }

            if (!Guid.TryParse(targetText, out Guid target))
            {
                Console.Error.WriteLine("Invalid --target GUID: " + targetText);
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

            string? pageSpec = parsed.Get("page");
            DrawingSurfaceModel? diagram;
            if (string.IsNullOrEmpty(pageSpec))
            {
                diagram = AuthoringSupport.FirstDiagram(model);
            }
            else if (AuthoringSupport.TryResolveDiagram(model, pageSpec!, out DrawingSurfaceModel? resolved, out string? pageError))
            {
                diagram = resolved;
            }
            else
            {
                Console.Error.WriteLine(pageError);
                return 1;
            }

            if (diagram == null)
            {
                Console.Error.WriteLine("The model has no diagram to connect within.");
                return 1;
            }

            if (!diagram.Borders.ContainsKey(source))
            {
                Console.Error.WriteLine("Source element not found: " + source);
                return 1;
            }

            if (!diagram.Borders.ContainsKey(target))
            {
                Console.Error.WriteLine("Target element not found: " + target);
                return 1;
            }

            DiagramEditor editor = new DiagramEditor(model);
            Guid id = editor.AddConnector(diagram, source, target);
            string? name = parsed.Get("name");
            if (!string.IsNullOrEmpty(name))
            {
                editor.SetElementName(diagram, id, name!);
            }

            if (parsed.Properties.Count > 0)
            {
                Entity? flow = DiagramEditor.FindElement(diagram, id);
                if (flow != null)
                {
                    if (!AuthoringSupport.TryApplyProperties(flow, parsed.Properties, "flow", parsed.HasFlag("force"), out string? propertyError, out IReadOnlyList<string> propertyWarnings))
                    {
                        Console.Error.WriteLine(propertyError);
                        return 1;
                    }

                    foreach (string warning in propertyWarnings)
                    {
                        Console.Error.WriteLine(warning);
                    }
                }
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
            Console.Error.WriteLine("  tmforge connect --source <guid> --target <guid> [--name <name>] [--page <name|index>] [--property KEY=VALUE]... [--json] <file>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Both endpoints must be on the same page; --page selects it (default: the first page).");
            Console.Error.WriteLine("Set flow properties the linter checks, e.g. --property Protocol=HTTPS --property Port=443 --property DataType=\"Customer Content\".");
            Console.Error.WriteLine("Mark a non-network flow to skip protocol/port/cleartext checks: --property Channel=In-Process|Local-file|Unix-socket|Loopback.");
            Console.Error.WriteLine("List every property and its allowed values with 'tmforge properties --base flow'.");
            Console.Error.WriteLine("Values are validated against the schema; pass --force to store an unknown property or value.");
        }
    }
}
