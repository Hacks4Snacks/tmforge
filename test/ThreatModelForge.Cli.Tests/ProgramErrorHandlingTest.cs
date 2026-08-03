namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for the dispatcher's handling of unusable input.
    /// <para>
    /// Eighteen verbs load a model through <c>CliModelLoader</c> and none of them guarded the load, so
    /// pointing any of them at a missing, corrupt, or simply non-model file produced an unhandled
    /// exception: a full crash dump, a stack trace, and exit 134. These tests pin the replacement
    /// contract — one line naming what is wrong, and exit 1 — and pin that a genuine defect is still
    /// allowed to crash rather than being disguised as the user's mistake.
    /// </para>
    /// </summary>
    [TestClass]
    public class ProgramErrorHandlingTest
    {
        /// <summary>Gets or sets the per-test temporary directory.</summary>
        public string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>Creates the temporary working directory.</summary>
        [TestInitialize]
        public void Initialize()
        {
            this.WorkingDirectory = Path.Join(Path.GetTempPath(), "tmforge-cli-errors-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.WorkingDirectory);
        }

        /// <summary>Removes the temporary working directory.</summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(this.WorkingDirectory))
            {
                Directory.Delete(this.WorkingDirectory, recursive: true);
            }
        }

        /// <summary>
        /// An authoring manifest is refused by name, with the command that turns it into a model.
        /// <para>
        /// This is the reported case: a manifest is valid tmforge input, just not a model, and the
        /// registry's generic "specify a format id" sent the reader looking for a format id that does
        /// not exist for manifests.
        /// </para>
        /// </summary>
        [TestMethod]
        public void OpeningAManifestNamesItAndPointsAtApply()
        {
            string manifest = @"{ ""schema"": ""tmforge-manifest"", ""version"": 1, ""name"": ""T"",
                ""elements"": [ { ""alias"": ""p1"", ""kind"": ""process"", ""name"": ""API"" } ] }";
            string path = this.Write("threat-model.tm.json", manifest);

            (int exitCode, string error) = Run("open", path);

            Assert.AreEqual(1, exitCode);
            StringAssert.Contains(error, "authoring manifest");
            StringAssert.Contains(error, "tmforge apply");
            Assert.IsFalse(error.Contains("Unhandled exception", StringComparison.Ordinal), error);
            Assert.IsFalse(error.Contains("   at ThreatModelForge", StringComparison.Ordinal), error);
        }

        /// <summary>
        /// A file that is neither a model nor a manifest still exits cleanly, reporting that no format
        /// could read it rather than crashing.
        /// </summary>
        [TestMethod]
        public void OpeningAnUnreadableDocumentReportsItWithoutCrashing()
        {
            string path = this.Write("notes.json", @"{ ""hello"": ""world"" }");

            (int exitCode, string error) = Run("open", path);

            Assert.AreEqual(1, exitCode);
            Assert.IsFalse(string.IsNullOrWhiteSpace(error));
            Assert.IsFalse(error.Contains("Unhandled exception", StringComparison.Ordinal), error);
        }

        /// <summary>
        /// A path that does not exist exits cleanly. Previously this surfaced the raw
        /// <see cref="FileNotFoundException"/> with a stack trace.
        /// </summary>
        [TestMethod]
        public void OpeningAMissingFileReportsItWithoutCrashing()
        {
            (int exitCode, string error) = Run("open", Path.Join(this.WorkingDirectory, "absent.tm7"));

            Assert.AreEqual(1, exitCode);
            Assert.IsFalse(string.IsNullOrWhiteSpace(error));
            Assert.IsFalse(error.Contains("Unhandled exception", StringComparison.Ordinal), error);
        }

        /// <summary>
        /// A file whose extension promises a model but whose bytes are not one exits cleanly. The
        /// deserializer's own failure type varies by format, which is why the classification covers the
        /// document-parsing family rather than one exception.
        /// </summary>
        [TestMethod]
        public void OpeningACorruptModelReportsItWithoutCrashing()
        {
            string path = this.Write("broken.tm7", "this is not XML at all");

            (int exitCode, string error) = Run("open", path);

            Assert.AreEqual(1, exitCode);
            Assert.IsFalse(string.IsNullOrWhiteSpace(error));
            Assert.IsFalse(error.Contains("Unhandled exception", StringComparison.Ordinal), error);
        }

        /// <summary>
        /// The classification accepts the failures that describe the user's own input.
        /// </summary>
        [TestMethod]
        public void InputFailuresAreClassifiedAsTheUsersInput()
        {
            Assert.IsTrue(Program.IsInputError(new NotSupportedException("no reader")));
            Assert.IsTrue(Program.IsInputError(new FileNotFoundException("gone")));
            Assert.IsTrue(Program.IsInputError(new UnauthorizedAccessException("denied")));
            Assert.IsTrue(Program.IsInputError(new InvalidDataException("corrupt container")));
            Assert.IsTrue(Program.IsInputError(new System.Text.Json.JsonException("bad json")));
            Assert.IsTrue(Program.IsInputError(new System.Xml.XmlException("bad xml")));
            Assert.IsTrue(Program.IsInputError(new ArgumentException("blank id")));
            Assert.IsTrue(Program.IsInputError(new FormatException("bad base64")));
        }

        /// <summary>
        /// A defect in the tool is NOT reported as the user's mistake. Without this the handler could
        /// widen to every exception and quietly turn crashes into one-line messages, which is exactly
        /// how a real bug goes unreported.
        /// </summary>
        [TestMethod]
        public void ToolDefectsAreNotClassifiedAsInput()
        {
            Assert.IsFalse(Program.IsInputError(new InvalidOperationException("bad state")));
            Assert.IsFalse(Program.IsInputError(new NullReferenceException()));
            Assert.IsFalse(Program.IsInputError(new IndexOutOfRangeException()));

            // Derives from ArgumentException, which is accepted: a null argument is this tool calling
            // itself wrongly, so it must stay loud.
            Assert.IsFalse(Program.IsInputError(new ArgumentNullException("model")));
        }

        /// <summary>
        /// Runs the dispatcher, capturing the exit code and everything written to standard error.
        /// </summary>
        /// <param name="args">The command-line arguments.</param>
        /// <returns>The exit code and the captured standard error.</returns>
        private static (int ExitCode, string Error) Run(params string[] args)
        {
            using StringWriter errorWriter = new StringWriter();
            TextWriter originalError = Console.Error;
            TextWriter originalOut = Console.Out;
            Console.SetError(errorWriter);
            Console.SetOut(TextWriter.Null);
            try
            {
                int exitCode = Program.Main(args);
                return (exitCode, errorWriter.ToString());
            }
            finally
            {
                Console.SetError(originalError);
                Console.SetOut(originalOut);
            }
        }

        /// <summary>Writes a file into the temporary working directory.</summary>
        /// <param name="name">The file name.</param>
        /// <param name="content">The file content.</param>
        /// <returns>The full path written.</returns>
        private string Write(string name, string content)
        {
            string path = Path.Join(this.WorkingDirectory, name);
            File.WriteAllText(path, content);
            return path;
        }
    }
}
