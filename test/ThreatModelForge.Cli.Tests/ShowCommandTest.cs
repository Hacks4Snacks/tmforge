namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for the <c>tmforge show</c> command (read-only element/flow inspection).
    /// </summary>
    [TestClass]
    public class ShowCommandTest
    {
        /// <summary>
        /// Gets or sets the working directory created for each test.
        /// </summary>
        private string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Gets the path of the model file created by <see cref="NewModel"/>.
        /// </summary>
        private string ModelPath => Path.Combine(this.WorkingDirectory, "model.tm7");

        /// <summary>
        /// Creates an isolated working directory for the test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            this.WorkingDirectory = Path.Combine(Path.GetTempPath(), "tmforge-show-" + Guid.NewGuid().ToString("N"));
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
        /// Verifies that <c>show</c> reports an element's kind, name, and custom properties.
        /// </summary>
        [TestMethod]
        public void ShowReportsNameKindAndProperties()
        {
            string path = this.NewModel();
            string id = this.AddElement("process", "API");
            Capture(() => SetCommand.Run(new[] { path, "--id", id, "--property", "AuthenticationScheme=OAuth" }));

            (int exit, string stdout) = Capture(() => ShowCommand.Run(new[] { path, "--id", id, "--json" }));

            Assert.AreEqual(0, exit);
            JsonElement data = JsonDocument.Parse(stdout).RootElement.GetProperty("data");
            Assert.AreEqual("element", data.GetProperty("kind").GetString());
            Assert.AreEqual("API", data.GetProperty("name").GetString());
            Assert.AreEqual("OAuth", data.GetProperty("properties").GetProperty("AuthenticationScheme").GetString());
        }

        /// <summary>
        /// Verifies that <c>show</c> surfaces the stencil id and its human-readable label.
        /// </summary>
        [TestMethod]
        public void ShowSurfacesStencilLabel()
        {
            string path = this.NewModel();
            string id = this.AddStencil("azure-sql");

            (int exit, string stdout) = Capture(() => ShowCommand.Run(new[] { path, "--id", id, "--json" }));

            Assert.AreEqual(0, exit);
            JsonElement data = JsonDocument.Parse(stdout).RootElement.GetProperty("data");
            Assert.AreEqual("azure-sql", data.GetProperty("stencil").GetString());
            Assert.AreEqual("Azure SQL Database", data.GetProperty("stencilLabel").GetString());
        }

        /// <summary>
        /// Verifies that <c>show</c> identifies a flow (connector) as such.
        /// </summary>
        [TestMethod]
        public void ShowFlowReportsFlowKind()
        {
            string path = this.NewModel();
            string source = this.AddElement("external", "Browser");
            string target = this.AddElement("process", "API");
            string flow = this.Connect(source, target);

            (int exit, string stdout) = Capture(() => ShowCommand.Run(new[] { path, "--id", flow, "--json" }));

            Assert.AreEqual(0, exit);
            Assert.AreEqual("flow", JsonDocument.Parse(stdout).RootElement.GetProperty("data").GetProperty("kind").GetString());
        }

        /// <summary>
        /// Verifies that showing an unknown identifier is reported as an error.
        /// </summary>
        [TestMethod]
        public void ShowUnknownIdReturnsError()
        {
            string path = this.NewModel();

            (int exit, _) = Capture(() => ShowCommand.Run(new[] { path, "--id", Guid.NewGuid().ToString() }));

            Assert.AreEqual(1, exit);
        }

        private static (int Exit, string Stdout) Capture(Func<int> run)
        {
            StringWriter outWriter = new StringWriter();
            StringWriter errorWriter = new StringWriter();
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

        private string NewModel()
        {
            Capture(() => NewCommand.Run(new[] { this.ModelPath, "--name", "Show Test" }));
            return this.ModelPath;
        }

        private string AddElement(string kind, string name)
        {
            (int exit, string stdout) = Capture(() => AddCommand.Run(new[] { kind, this.ModelPath, "--name", name, "--json" }));
            Assert.AreEqual(0, exit);
            return JsonDocument.Parse(stdout).RootElement.GetProperty("data").GetProperty("id").GetGuid().ToString();
        }

        private string AddStencil(string stencil)
        {
            (int exit, string stdout) = Capture(() => AddCommand.Run(new[] { "--stencil", stencil, this.ModelPath, "--json" }));
            Assert.AreEqual(0, exit);
            return JsonDocument.Parse(stdout).RootElement.GetProperty("data").GetProperty("id").GetGuid().ToString();
        }

        private string Connect(string source, string target)
        {
            (int exit, string stdout) = Capture(() => ConnectCommand.Run(new[] { this.ModelPath, "--source", source, "--target", target, "--json" }));
            Assert.AreEqual(0, exit);
            return JsonDocument.Parse(stdout).RootElement.GetProperty("data").GetProperty("id").GetGuid().ToString();
        }
    }
}
