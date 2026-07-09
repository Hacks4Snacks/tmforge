namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for the <see cref="ConvertCommand"/> class.
    /// </summary>
    [TestClass]
    public class ConvertCommandTest
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
            this.WorkingDirectory = Path.Join(Path.GetTempPath(), "tmforge-convert-" + Guid.NewGuid().ToString("N"));
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
        /// Verifies that converting a tmforge-json document to draw.io writes a well-formed mxGraph
        /// file, with the target inferred from the <c>-out</c> extension.
        /// </summary>
        [TestMethod]
        public void ConvertsToDrawioFromOutputExtension()
        {
            string input = this.WriteInput();
            string output = Path.Join(this.WorkingDirectory, "model.drawio");

            int exit = ConvertCommand.Run(new[] { "--out", output, input });

            Assert.AreEqual(0, exit);
            Assert.IsTrue(File.Exists(output));
            StringAssert.Contains(File.ReadAllText(output), "<mxfile");
        }

        /// <summary>
        /// Verifies that converting to Visio via <c>--to vsdx</c> writes an OPC (zip) package.
        /// </summary>
        [TestMethod]
        public void ConvertsToVsdxByTargetId()
        {
            string input = this.WriteInput();
            string output = Path.Join(this.WorkingDirectory, "model.vsdx");

            int exit = ConvertCommand.Run(new[] { "--to", "vsdx", "--out", output, input });

            Assert.AreEqual(0, exit);
            byte[] bytes = File.ReadAllBytes(output);
            Assert.IsTrue(bytes.Length > 4 && bytes[0] == (byte)'P' && bytes[1] == (byte)'K', "a .vsdx package is a zip");
        }

        /// <summary>
        /// Verifies that a missing input file is reported as an error.
        /// </summary>
        [TestMethod]
        public void MissingInputReturnsError()
        {
            int exit = ConvertCommand.Run(new[] { "--to", "drawio", Path.Join(this.WorkingDirectory, "does-not-exist.tm7") });

            Assert.AreEqual(1, exit);
        }

        /// <summary>
        /// Verifies that omitting both the target id and an output path is an error.
        /// </summary>
        [TestMethod]
        public void MissingTargetReturnsError()
        {
            string input = this.WriteInput();

            int exit = ConvertCommand.Run(new[] { input });

            Assert.AreEqual(1, exit);
        }

        /// <summary>
        /// Verifies that the canonical grammar plus <c>--json</c> converts and emits a result envelope.
        /// </summary>
        [TestMethod]
        public void CanonicalGrammarWithJsonEmitsEnvelope()
        {
            string input = this.WriteInput();
            string output = Path.Join(this.WorkingDirectory, "model.drawio");

            using StringWriter writer = new StringWriter();
            TextWriter original = Console.Out;
            Console.SetOut(writer);
            int exit;
            try
            {
                exit = ConvertCommand.Run(new[] { "--to", "drawio", "--out", output, "--json", input });
            }
            finally
            {
                Console.SetOut(original);
            }

            Assert.AreEqual(0, exit);
            Assert.IsTrue(File.Exists(output));
            using JsonDocument document = JsonDocument.Parse(writer.ToString());
            Assert.AreEqual("convert", document.RootElement.GetProperty("command").GetString());
            Assert.AreEqual("drawio", document.RootElement.GetProperty("data").GetProperty("format").GetString());
        }

        private string WriteInput()
        {
            string input = Path.Join(this.WorkingDirectory, "model.tmforge.json");
            File.WriteAllText(input, SampleJson);
            return input;
        }
    }
}
