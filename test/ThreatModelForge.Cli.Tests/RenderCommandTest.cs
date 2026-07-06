namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for the <see cref="RenderCommand"/> class. See ADR 0016.
    /// </summary>
    [TestClass]
    public class RenderCommandTest
    {
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
            this.WorkingDirectory = Path.Combine(Path.GetTempPath(), "tmforge-render-" + Guid.NewGuid().ToString("N"));
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
        /// Verifies that <c>--plain</c> rendering includes the diagram header and each element name.
        /// </summary>
        [TestMethod]
        public void PlainRenderIncludesElementNames()
        {
            string path = this.NewModel();
            Capture(() => AddCommand.Run(new[] { "process", path, "--name", "Web API" }));
            Capture(() => AddCommand.Run(new[] { "store", path, "--name", "Database" }));

            (int exit, string stdout) = Capture(() => RenderCommand.Run(new[] { "--plain", path }));

            Assert.AreEqual(0, exit);
            StringAssert.Contains(stdout, "Diagram:");
            StringAssert.Contains(stdout, "Web API");
            StringAssert.Contains(stdout, "Database");
        }

        /// <summary>
        /// Verifies that <c>--plain</c> output contains no ANSI escape sequences.
        /// </summary>
        [TestMethod]
        public void PlainRenderHasNoAnsiEscapes()
        {
            string path = this.NewModel();
            Capture(() => AddCommand.Run(new[] { "process", path, "--name", "API" }));

            (int exit, string stdout) = Capture(() => RenderCommand.Run(new[] { "--plain", path }));

            Assert.AreEqual(0, exit);
            Assert.IsFalse(stdout.Contains('\u001b'), "plain output should not contain ANSI escapes");
        }

        /// <summary>
        /// Verifies that an empty model renders without error.
        /// </summary>
        [TestMethod]
        public void EmptyModelRendersWithoutError()
        {
            string path = this.NewModel();

            (int exit, string stdout) = Capture(() => RenderCommand.Run(new[] { "--plain", path }));

            Assert.AreEqual(0, exit);
            StringAssert.Contains(stdout, "Diagram:");
        }

        /// <summary>
        /// Verifies that a missing input file is reported as an error.
        /// </summary>
        [TestMethod]
        public void MissingInputReturnsError()
        {
            (int exit, _) = Capture(() => RenderCommand.Run(new[] { "--plain", Path.Combine(this.WorkingDirectory, "does-not-exist.tm7") }));

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
            string path = Path.Combine(this.WorkingDirectory, "model.tm7");
            Capture(() => NewCommand.Run(new[] { path, "--name", "Render Test" }));
            return path;
        }
    }
}
