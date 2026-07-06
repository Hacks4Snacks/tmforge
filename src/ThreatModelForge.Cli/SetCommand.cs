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
    /// Implements <c>tmforge set</c>: sets the name and/or custom properties of an existing element
    /// or flow (identified by GUID), so an agent can resolve linter findings (for example
    /// <c>Protocol</c>, <c>Port</c>, <c>DataType</c>, <c>AuthenticationScheme</c>) without
    /// hand-editing the <c>.tm7</c>.
    /// </summary>
    internal static class SetCommand
    {
        /// <summary>
        /// Runs the set command.
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

            CliArgs parsed = CliArgs.Parse(args, new[] { "id", "name" }, new[] { "force" });
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

            string? name = parsed.Get("name");
            if (string.IsNullOrEmpty(name) && parsed.Properties.Count == 0)
            {
                Console.Error.WriteLine("Nothing to set: supply --name and/or --property KEY=VALUE.");
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

            Entity? target = null;
            foreach (DrawingSurfaceModel diagram in model.DrawingSurfaceList)
            {
                target = DiagramEditor.FindElement(diagram, id);
                if (target != null)
                {
                    break;
                }
            }

            if (target == null)
            {
                Console.Error.WriteLine("Element not found: " + id);
                return 1;
            }

            if (!string.IsNullOrEmpty(name))
            {
                DiagramElementHelper.SetName(target, name!);
            }

            if (!AuthoringSupport.TryApplyProperties(target, parsed.Properties, AuthoringSupport.SchemaBase(target), parsed.HasFlag("force"), out string? propertyError, out IReadOnlyList<string> propertyWarnings))
            {
                Console.Error.WriteLine(propertyError);
                return 1;
            }

            foreach (string warning in propertyWarnings)
            {
                Console.Error.WriteLine(warning);
            }

            AuthoringSupport.Save(model, input!, format);

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("set", new
                {
                    id,
                    name = DiagramElementHelper.GetName(target),
                    properties = DiagramElementHelper.GetCustomProperties(target),
                });
            }
            else
            {
                Console.Error.WriteLine("Updated " + id + " in " + input + ".");
            }

            return 0;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Set the name and/or custom properties of an existing element or flow.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge set --id <guid> [--name <name>] [--property KEY=VALUE]... [--json] <file>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Resolve linter findings, e.g. --property Protocol=HTTPS --property Port=443,");
            Console.Error.WriteLine("--property DataType=\"Customer Content\", or --property AuthenticationScheme=OAuth.");
            Console.Error.WriteLine("Mark a non-network flow with --property Channel=In-Process|Local-file|Unix-socket|Loopback.");
            Console.Error.WriteLine("List every property and its allowed values with 'tmforge properties'.");
            Console.Error.WriteLine("Values are validated against the schema; pass --force to store an unknown property or value.");
        }
    }
}
