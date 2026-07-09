namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for the <c>tmforge schema</c> command (the machine-readable output contract).
    /// </summary>
    [TestClass]
    public class SchemaCommandTest
    {
        /// <summary>
        /// The human listing describes the envelope and includes the authoring verbs' output fields.
        /// </summary>
        [TestMethod]
        public void HumanListsEnvelopeAndCommands()
        {
            (int exit, string stdout) = Capture(() => SchemaCommand.Run(Array.Empty<string>()));

            Assert.AreEqual(0, exit);
            StringAssert.Contains(stdout, "schemaVersion");
            StringAssert.Contains(stdout, "COMMAND");
            StringAssert.Contains(stdout, "add");
            StringAssert.Contains(stdout, "connect");
        }

        /// <summary>
        /// The <c>--json</c> form emits the envelope contract and a command catalog whose entries carry
        /// the <c>data</c> field summary an agent needs (for example <c>add</c> returning <c>id</c>).
        /// </summary>
        [TestMethod]
        public void JsonEmitsEnvelopeAndCommandCatalog()
        {
            (int exit, string stdout) = Capture(() => SchemaCommand.Run(new[] { "--json" }));

            Assert.AreEqual(0, exit);
            using JsonDocument document = JsonDocument.Parse(stdout);
            JsonElement data = document.RootElement.GetProperty("data");
            Assert.AreEqual(1, data.GetProperty("envelope").GetProperty("schemaVersion").GetInt32());

            bool foundAdd = false;
            foreach (JsonElement entry in data.GetProperty("commands").EnumerateArray()
                .Where(entry => entry.GetProperty("command").GetString() == "add"))
            {
                StringAssert.Contains(entry.GetProperty("data").GetString(), "id");
                foundAdd = true;
            }

            Assert.IsTrue(foundAdd, "the catalog must document the add command's output");
        }

        private static (int Exit, string Stdout) Capture(Func<int> run)
        {
            using StringWriter outWriter = new StringWriter();
            using StringWriter errorWriter = new StringWriter();
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);
            try
            {
                int exit = run();
                return (exit, outWriter.ToString());
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }
        }
    }
}
