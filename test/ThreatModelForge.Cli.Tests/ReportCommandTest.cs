namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for the <see cref="ReportCommand"/> class.
    /// </summary>
    [TestClass]
    public class ReportCommandTest
    {
        private const string SampleJson =
            "{\"schema\":\"tmforge-json\",\"version\":\"0.1\"," +
            "\"elements\":[" +
            "{\"id\":\"p1\",\"kind\":\"process\",\"name\":\"API\",\"x\":100,\"y\":100}," +
            "{\"id\":\"ds1\",\"kind\":\"datastore\",\"name\":\"DB\",\"x\":400,\"y\":100}]," +
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
            this.WorkingDirectory = Path.Combine(Path.GetTempPath(), "tmforge-report-" + Guid.NewGuid().ToString("N"));
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
        /// Verifies that <c>--json</c> with <c>--out</c> writes the HTML and emits a result envelope.
        /// </summary>
        [TestMethod]
        public void JsonEmitsEnvelope()
        {
            string input = this.WriteInput();
            string output = Path.Combine(this.WorkingDirectory, "report.html");

            (int exit, string stdout) = Run(new[] { "--out", output, "--json", input });

            Assert.AreEqual(0, exit);
            Assert.IsTrue(File.Exists(output));
            using JsonDocument document = JsonDocument.Parse(stdout);
            JsonElement root = document.RootElement;
            Assert.AreEqual("report", root.GetProperty("command").GetString());
            JsonElement data = root.GetProperty("data");
            Assert.AreEqual("html", data.GetProperty("format").GetString());
            Assert.IsTrue(data.GetProperty("bytes").GetInt32() > 0);
        }

        /// <summary>
        /// Verifies that, without <c>--out</c>, the HTML report is written to standard output.
        /// </summary>
        [TestMethod]
        public void WritesHtmlToStandardOutputByDefault()
        {
            string input = this.WriteInput();

            (int exit, string stdout) = Run(new[] { input });

            Assert.AreEqual(0, exit);
            StringAssert.Contains(stdout, "<");
        }

        /// <summary>
        /// Verifies that a missing input file is reported as an error.
        /// </summary>
        [TestMethod]
        public void MissingInputReturnsError()
        {
            (int exit, _) = Run(new[] { Path.Combine(this.WorkingDirectory, "does-not-exist.tm7") });

            Assert.AreEqual(1, exit);
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
                int exit = ReportCommand.Run(args);
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
