namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests that guard the single-source-of-truth command catalog: the dispatcher, the
    /// <c>--help</c> usage, and <c>tmforge schema</c> are all generated from <see cref="CommandCatalog"/>,
    /// so a newly added verb cannot drift out of sync across them.
    /// </summary>
    [TestClass]
    public class CommandCatalogTest
    {
        /// <summary>
        /// Every registered verb is unique and carries a usage summary.
        /// </summary>
        [TestMethod]
        public void VerbsAreUniqueAndDescribed()
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (CommandInfo command in CommandCatalog.Commands)
            {
                Assert.IsTrue(seen.Add(command.Verb), "duplicate verb: " + command.Verb);
                Assert.IsFalse(string.IsNullOrWhiteSpace(command.Summary), command.Verb + " needs a usage summary");
            }
        }

        /// <summary>
        /// Every registered verb resolves through the catalog's dispatch lookup.
        /// </summary>
        [TestMethod]
        public void EveryCommandIsDispatchable()
        {
            foreach (CommandInfo command in CommandCatalog.Commands)
            {
                Assert.AreSame(command, CommandCatalog.Find(command.Verb), command.Verb + " must be dispatchable");
            }
        }

        /// <summary>
        /// The <c>--help</c> usage lists every registered verb (it is generated from the catalog).
        /// </summary>
        [TestMethod]
        public void UsageListsEveryCommand()
        {
            string usage = CaptureError(() => Program.Main(Array.Empty<string>()));

            foreach (CommandInfo command in CommandCatalog.Commands)
            {
                StringAssert.Contains(usage, command.Verb, "usage must list " + command.Verb);
            }
        }

        /// <summary>
        /// <c>tmforge schema --json</c> documents every verb that produces machine-readable output (it
        /// is generated from the catalog), so the schema cannot drift when a verb is added.
        /// </summary>
        [TestMethod]
        public void SchemaDocumentsEveryJsonCommand()
        {
            string stdout = CaptureOut(() => SchemaCommand.Run(new[] { "--json" }));

            using JsonDocument document = JsonDocument.Parse(stdout);
            HashSet<string> listed = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement entry in document.RootElement.GetProperty("data").GetProperty("commands").EnumerateArray())
            {
                listed.Add(entry.GetProperty("command").GetString() ?? string.Empty);
            }

            foreach (CommandInfo command in CommandCatalog.Commands)
            {
                bool documented = command.JsonData == null || listed.Contains(command.Verb);
                Assert.IsTrue(documented, "schema must document " + command.Verb);
            }
        }

        private static string CaptureOut(Func<int> run)
        {
            StringWriter outWriter = new StringWriter();
            TextWriter original = Console.Out;
            Console.SetOut(outWriter);
            try
            {
                run();
                return outWriter.ToString();
            }
            finally
            {
                Console.SetOut(original);
            }
        }

        private static string CaptureError(Func<int> run)
        {
            StringWriter errorWriter = new StringWriter();
            TextWriter original = Console.Error;
            Console.SetError(errorWriter);
            try
            {
                run();
                return errorWriter.ToString();
            }
            finally
            {
                Console.SetError(original);
            }
        }
    }
}
