namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for the <see cref="AnalyzeCommand"/> class.
    /// </summary>
    [TestClass]
    public class AnalyzeCommandTest
    {
        private const string SampleJson =
            "{\"schema\":\"tmforge-json\",\"version\":\"0.1\"," +
            "\"elements\":[" +
            "{\"id\":\"p1\",\"kind\":\"process\",\"name\":\"API\",\"x\":100,\"y\":100}," +
            "{\"id\":\"ds1\",\"kind\":\"datastore\",\"name\":\"DB\",\"x\":400,\"y\":100}]," +
            "\"flows\":[{\"id\":\"f1\",\"source\":\"p1\",\"target\":\"ds1\",\"name\":\"query\"}]}";

        private const string SampleJsonAllPacksDisabled =
            "{\"schema\":\"tmforge-json\",\"version\":\"0.1\"," +
            "\"elements\":[" +
            "{\"id\":\"p1\",\"kind\":\"process\",\"name\":\"API\",\"x\":100,\"y\":100}," +
            "{\"id\":\"ds1\",\"kind\":\"datastore\",\"name\":\"DB\",\"x\":400,\"y\":100}]," +
            "\"flows\":[{\"id\":\"f1\",\"source\":\"p1\",\"target\":\"ds1\",\"name\":\"query\"}]," +
            "\"analysis\":{\"disabledPacks\":[\"core-hygiene\",\"stride-completeness\",\"input-validation\",\"data-protection\",\"transport-security\",\"identity-access\"]}}";

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
            this.WorkingDirectory = Path.Combine(Path.GetTempPath(), "tmforge-analyze-" + Guid.NewGuid().ToString("N"));
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
        /// Verifies that a model with error-severity findings returns the findings exit code (2).
        /// </summary>
        [TestMethod]
        public void ModelWithFindingsExitsTwo()
        {
            string input = this.WriteInput();

            (int exit, _) = Run(new[] { input });

            Assert.AreEqual(2, exit);
        }

        /// <summary>
        /// Verifies that <c>--json</c> emits the versioned envelope and still returns the findings exit code.
        /// </summary>
        [TestMethod]
        public void JsonEmitsEnvelope()
        {
            string input = this.WriteInput();

            (int exit, string output) = Run(new[] { "--json", input });

            Assert.AreEqual(2, exit);
            using JsonDocument document = JsonDocument.Parse(output);
            JsonElement root = document.RootElement;
            Assert.AreEqual(1, root.GetProperty("schemaVersion").GetInt32());
            Assert.AreEqual("analyze", root.GetProperty("command").GetString());
            Assert.IsTrue(root.GetProperty("data").TryGetProperty("sourcePath", out _));
        }

        /// <summary>
        /// Verifies that a missing input file returns the error exit code (1), distinct from findings.
        /// </summary>
        [TestMethod]
        public void MissingInputReturnsError()
        {
            (int exit, _) = Run(new[] { Path.Combine(this.WorkingDirectory, "does-not-exist.tm7") });

            Assert.AreEqual(1, exit);
        }

        /// <summary>
        /// Verifies the CLI honors the model-borne analysis selection: a model whose embedded
        /// selection disables every rule pack produces no findings and analyzes clean (exit 0).
        /// </summary>
        [TestMethod]
        public void ModelBorneSelectionDisablesRules()
        {
            string input = Path.Combine(this.WorkingDirectory, "model.tmforge.json");
            File.WriteAllText(input, SampleJsonAllPacksDisabled);

            (int exit, _) = Run(new[] { input });

            Assert.AreEqual(0, exit);
        }

        private static (int Exit, string Stdout) Run(string[] args)
        {
            StringWriter outWriter = new StringWriter();
            StringWriter errorWriter = new StringWriter();
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);
            try
            {
                int exit = AnalyzeCommand.Run(args);
                return (exit, outWriter.ToString());
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
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
