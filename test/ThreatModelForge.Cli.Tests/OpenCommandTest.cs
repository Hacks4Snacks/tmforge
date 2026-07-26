namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for the <see cref="OpenCommand"/> class.
    /// </summary>
    [TestClass]
    public class OpenCommandTest
    {
        private const string SampleJson =
            "{\"schema\":\"tmforge-json\",\"version\":\"0.1\"," +
            "\"elements\":[" +
            "{\"id\":\"p1\",\"kind\":\"process\",\"name\":\"Web App\",\"x\":100,\"y\":100}," +
            "{\"id\":\"ds1\",\"kind\":\"datastore\",\"name\":\"Database\",\"x\":420,\"y\":100}]," +
            "\"flows\":[{\"id\":\"f1\",\"source\":\"p1\",\"target\":\"ds1\",\"name\":\"query\"}]}";

        /// <summary>
        /// Gets or sets the working directory created for each test.
        /// </summary>
        private string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Creates an isolated working directory for the test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            this.WorkingDirectory = Path.Join(Path.GetTempPath(), "tmforge-open-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.WorkingDirectory);
        }

        /// <summary>
        /// Removes the working directory after the test.
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(this.WorkingDirectory))
            {
                Directory.Delete(this.WorkingDirectory, recursive: true);
            }
        }

        /// <summary>
        /// Verifies that the human-readable summary reports the element and flow counts.
        /// </summary>
        [TestMethod]
        public void SummaryReportsElementAndFlowCounts()
        {
            string input = this.WriteInput();

            (int exit, string output) = Capture(new[] { input });

            Assert.AreEqual(0, exit);
            StringAssert.Contains(output, "2 components, 1 flows");
            StringAssert.Contains(output, "Threats:  0");
        }

        /// <summary>
        /// Verifies that <c>--json</c> emits the versioned envelope with the expected counts.
        /// </summary>
        [TestMethod]
        public void JsonEmitsVersionedEnvelope()
        {
            string input = this.WriteInput();

            (int exit, string output) = Capture(new[] { "--json", input });

            Assert.AreEqual(0, exit);
            using JsonDocument document = JsonDocument.Parse(output);
            JsonElement root = document.RootElement;
            Assert.AreEqual(1, root.GetProperty("schemaVersion").GetInt32());
            Assert.AreEqual("open", root.GetProperty("command").GetString());
            JsonElement data = root.GetProperty("data");
            Assert.AreEqual(2, data.GetProperty("componentCount").GetInt32());
            Assert.AreEqual(1, data.GetProperty("connectorCount").GetInt32());
            Assert.AreEqual(0, data.GetProperty("threatCount").GetInt32());
        }

        /// <summary>
        /// The threat total is split by origin and standing, so a reader can tell an authored entry
        /// from a rule-derived one and a live finding from a leftover.
        /// </summary>
        [TestMethod]
        public void JsonSplitsThreatCountsByOriginAndStanding()
        {
            string input = this.WriteInput();

            (int exit, string output) = Capture(new[] { "--json", input });

            Assert.AreEqual(0, exit);
            using JsonDocument document = JsonDocument.Parse(output);
            JsonElement threats = document.RootElement.GetProperty("data").GetProperty("threats");

            Assert.AreEqual(0, threats.GetProperty("total").GetInt32());
            Assert.AreEqual(0, threats.GetProperty("manual").GetInt32());
            Assert.AreEqual(0, threats.GetProperty("persistedGenerated").GetInt32());
            Assert.AreEqual(0, threats.GetProperty("staleGenerated").GetInt32());
            Assert.AreEqual(0, threats.GetProperty("indeterminateGenerated").GetInt32());
            Assert.AreEqual(0, threats.GetProperty("unavailableRuleIds").GetArrayLength());

            // The sample model trips real rules, so the current-generated count must reflect what the
            // rules produce rather than what the register happens to store.
            Assert.IsTrue(
                threats.GetProperty("currentGenerated").GetInt32() > 0,
                "The current rules produce threats for this model.");
        }

        /// <summary>
        /// A stored entry the current rules still produce counts as persisted and current, and is not
        /// reported as stale. This uses a <c>.tm7</c> because that is the format carrying a full threat
        /// register; tmforge-json deliberately persists only author-owned state.
        /// </summary>
        [TestMethod]
        public void PersistedRegisterIsReportedAsCurrentNotStale()
        {
            string input = this.WriteTm7Input();
            Assert.AreEqual(0, ThreatsCommand.Run(new[] { input, "--write" }));

            (int exit, string output) = Capture(new[] { "--json", input });

            Assert.AreEqual(0, exit);
            using JsonDocument document = JsonDocument.Parse(output);
            JsonElement threats = document.RootElement.GetProperty("data").GetProperty("threats");

            int persisted = threats.GetProperty("persistedGenerated").GetInt32();
            Assert.IsTrue(persisted > 0, "--write must persist the generated register.");
            Assert.AreEqual(persisted, threats.GetProperty("currentGenerated").GetInt32());
            Assert.AreEqual(0, threats.GetProperty("staleGenerated").GetInt32());
            Assert.AreEqual(0, threats.GetProperty("manual").GetInt32());
        }

        /// <summary>
        /// Verifies that a missing input file is reported as an error.
        /// </summary>
        [TestMethod]
        public void MissingInputReturnsError()
        {
            (int exit, _) = Capture(new[] { Path.Join(this.WorkingDirectory, "does-not-exist.tm7") });

            Assert.AreEqual(1, exit);
        }

        private static (int Exit, string Output) Capture(string[] args)
        {
            using StringWriter writer = new StringWriter();
            TextWriter original = Console.Out;
            Console.SetOut(writer);
            try
            {
                int exit = OpenCommand.Run(args);
                return (exit, writer.ToString());
            }
            finally
            {
                Console.SetOut(original);
            }
        }

        private string WriteInput()
        {
            string input = Path.Join(this.WorkingDirectory, "model.tmforge.json");
            File.WriteAllText(input, SampleJson);
            return input;
        }

        private string WriteTm7Input()
        {
            string source = this.WriteInput();
            string output = Path.Join(this.WorkingDirectory, "model.tm7");
            Assert.AreEqual(0, ConvertCommand.Run(new[] { "--to", "tm7", "--out", output, source }));
            return output;
        }
    }
}
