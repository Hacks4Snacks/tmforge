namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for the <see cref="ListCommand"/> class.
    /// </summary>
    [TestClass]
    public class ListCommandTest
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
            this.WorkingDirectory = Path.Combine(Path.GetTempPath(), "tmforge-list-" + Guid.NewGuid().ToString("N"));
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
        /// Verifies that listing components prints each component's name.
        /// </summary>
        [TestMethod]
        public void ListComponentsShowsNames()
        {
            string input = this.WriteInput();

            (int exit, string output) = Capture(new[] { "components", input });

            Assert.AreEqual(0, exit);
            StringAssert.Contains(output, "Web App");
            StringAssert.Contains(output, "Database");
        }

        /// <summary>
        /// Verifies that listing flows as JSON reports the kind and count in the envelope.
        /// </summary>
        [TestMethod]
        public void ListFlowsJsonReportsKindAndCount()
        {
            string input = this.WriteInput();

            (int exit, string output) = Capture(new[] { "flows", "--json", input });

            Assert.AreEqual(0, exit);
            using JsonDocument document = JsonDocument.Parse(output);
            JsonElement data = document.RootElement.GetProperty("data");
            Assert.AreEqual("flows", data.GetProperty("kind").GetString());
            Assert.AreEqual(1, data.GetProperty("count").GetInt32());
        }

        /// <summary>
        /// Verifies that listing threats on a model without threats reports the empty state.
        /// </summary>
        [TestMethod]
        public void ListThreatsReportsEmptyState()
        {
            string input = this.WriteInput();

            (int exit, string output) = Capture(new[] { "threats", input });

            Assert.AreEqual(0, exit);
            StringAssert.Contains(output, "No threats found.");
        }

        /// <summary>
        /// Verifies that an unrecognized list type is reported as an error.
        /// </summary>
        [TestMethod]
        public void UnknownNounReturnsError()
        {
            string input = this.WriteInput();

            (int exit, _) = Capture(new[] { "widgets", input });

            Assert.AreEqual(1, exit);
        }

        /// <summary>
        /// Verifies that a missing input file is reported as an error.
        /// </summary>
        [TestMethod]
        public void MissingInputReturnsError()
        {
            (int exit, _) = Capture(new[] { "components", Path.Combine(this.WorkingDirectory, "does-not-exist.tm7") });

            Assert.AreEqual(1, exit);
        }

        private static (int Exit, string Output) Capture(string[] args)
        {
            StringWriter writer = new StringWriter();
            TextWriter original = Console.Out;
            Console.SetOut(writer);
            try
            {
                int exit = ListCommand.Run(args);
                return (exit, writer.ToString());
            }
            finally
            {
                Console.SetOut(original);
            }
        }

        private string WriteInput()
        {
            string input = Path.Combine(this.WorkingDirectory, "model.tmforge.json");
            File.WriteAllText(input, SampleJson);
            return input;
        }
    }
}
