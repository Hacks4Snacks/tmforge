namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Engine;

    /// <summary>
    /// Unit tests for alias-based element references and deterministic, stable element ids
    /// (<c>add --alias</c>, and alias/name resolution in <c>connect</c>/<c>set</c>/<c>show</c>/
    /// <c>remove</c>/<c>rename</c>).
    /// </summary>
    [TestClass]
    public class AliasResolutionTest
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
            this.WorkingDirectory = Path.Join(Path.GetTempPath(), "tmforge-alias-" + Guid.NewGuid().ToString("N"));
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
        /// An aliased element gets a deterministic id derived from the alias, stable across rebuilds
        /// (the same alias in two freshly created models yields the same id).
        /// </summary>
        [TestMethod]
        public void AddAliasProducesDeterministicStableId()
        {
            string first = this.NewModel("first.tm7");
            string second = this.NewModel("second.tm7");

            string idA = AddAlias("process", first, "UserData reconciler", "P1");
            string idB = AddAlias("process", second, "Something else entirely", "P1");

            Assert.AreEqual(idA, idB, "the same alias must map to the same id across models/rebuilds");
            Assert.AreEqual(AuthoringSupport.DeterministicId("P1"), Guid.Parse(idA));
        }

        /// <summary>
        /// <c>connect</c> resolves both endpoints by alias.
        /// </summary>
        [TestMethod]
        public void ConnectResolvesEndpointsByAlias()
        {
            string path = this.NewModel("model.tm7");
            AddAlias("process", path, "Reconciler", "P1");
            AddAlias("store", path, "ConfigMap", "CUD");

            (int exit, _) = Capture(() => ConnectCommand.Run(new[] { path, "--source", "P1", "--target", "CUD", "--name", "F5" }));

            Assert.AreEqual(0, exit);
            Assert.AreEqual(1, OpenData(path).GetProperty("connectorCount").GetInt32());
        }

        /// <summary>
        /// <c>connect</c> resolves an endpoint by its unique element name when no alias is set.
        /// </summary>
        [TestMethod]
        public void ConnectResolvesEndpointsByUniqueName()
        {
            string path = this.NewModel("model.tm7");
            AddElement("process", path, "Web API");
            AddElement("store", path, "Database");

            (int exit, _) = Capture(() => ConnectCommand.Run(new[] { path, "--source", "Web API", "--target", "Database" }));

            Assert.AreEqual(0, exit);
            Assert.AreEqual(1, OpenData(path).GetProperty("connectorCount").GetInt32());
        }

        /// <summary>
        /// <c>set</c> and <c>show</c> resolve an element by alias.
        /// </summary>
        [TestMethod]
        public void SetAndShowResolveByAlias()
        {
            string path = this.NewModel("model.tm7");
            AddAlias("process", path, "Reconciler", "P4");

            (int setExit, _) = Capture(() => SetCommand.Run(new[] { path, "--id", "P4", "--property", "AuthenticationScheme=OAuth" }));
            Assert.AreEqual(0, setExit);

            (int showExit, string showOut) = Capture(() => ShowCommand.Run(new[] { path, "--id", "P4", "--json" }));
            Assert.AreEqual(0, showExit);
            using JsonDocument document = JsonDocument.Parse(showOut);
            JsonElement properties = document.RootElement.GetProperty("data").GetProperty("properties");
            Assert.AreEqual("OAuth", properties.GetProperty("AuthenticationScheme").GetString());
        }

        /// <summary>
        /// <c>rename</c> resolves an element by alias.
        /// </summary>
        [TestMethod]
        public void RenameResolvesByAlias()
        {
            string path = this.NewModel("model.tm7");
            AddAlias("process", path, "Original", "P1");

            (int exit, _) = Capture(() => RenameCommand.Run(new[] { path, "--id", "P1", "--name", "Renamed" }));

            Assert.AreEqual(0, exit);
            (int showExit, string showOut) = Capture(() => ShowCommand.Run(new[] { path, "--id", "P1", "--json" }));
            Assert.AreEqual(0, showExit);
            using JsonDocument document = JsonDocument.Parse(showOut);
            Assert.AreEqual("Renamed", document.RootElement.GetProperty("data").GetProperty("name").GetString());
        }

        /// <summary>
        /// <c>remove</c> resolves an element by alias.
        /// </summary>
        [TestMethod]
        public void RemoveResolvesByAlias()
        {
            string path = this.NewModel("model.tm7");
            AddAlias("process", path, "Reconciler", "P1");

            (int exit, _) = Capture(() => RemoveCommand.Run(new[] { path, "--id", "P1" }));

            Assert.AreEqual(0, exit);
            Assert.AreEqual(0, OpenData(path).GetProperty("componentCount").GetInt32());
        }

        /// <summary>
        /// Reusing an alias within a model is rejected (aliases must be unique).
        /// </summary>
        [TestMethod]
        public void DuplicateAliasReturnsError()
        {
            string path = this.NewModel("model.tm7");
            AddAlias("process", path, "First", "P1");

            (int exit, _) = Capture(() => AddCommand.Run(new[] { "process", path, "--name", "Second", "--alias", "P1" }));

            Assert.AreEqual(1, exit);
        }

        /// <summary>
        /// An ambiguous name (matching more than one element) is rejected; the author must use an
        /// alias or GUID.
        /// </summary>
        [TestMethod]
        public void AmbiguousNameReturnsError()
        {
            string path = this.NewModel("model.tm7");
            AddElement("process", path, "Duplicate");
            AddElement("process", path, "Duplicate");

            (int exit, _) = Capture(() => ShowCommand.Run(new[] { path, "--id", "Duplicate", "--json" }));

            Assert.AreEqual(1, exit);
        }

        private static string AddElement(string kind, string path, string name)
        {
            (int exit, string stdout) = Capture(() => AddCommand.Run(new[] { kind, path, "--name", name, "--json" }));
            Assert.AreEqual(0, exit);
            using JsonDocument document = JsonDocument.Parse(stdout);
            return document.RootElement.GetProperty("data").GetProperty("id").GetString() ?? string.Empty;
        }

        private static string AddAlias(string kind, string path, string name, string alias)
        {
            (int exit, string stdout) = Capture(() => AddCommand.Run(new[] { kind, path, "--name", name, "--alias", alias, "--json" }));
            Assert.AreEqual(0, exit);
            using JsonDocument document = JsonDocument.Parse(stdout);
            JsonElement data = document.RootElement.GetProperty("data");
            Assert.AreEqual(alias, data.GetProperty("alias").GetString());
            return data.GetProperty("id").GetString() ?? string.Empty;
        }

        private static JsonElement OpenData(string path)
        {
            (int exit, string stdout) = Capture(() => OpenCommand.Run(new[] { "--json", path }));
            Assert.AreEqual(0, exit);
            using JsonDocument document = JsonDocument.Parse(stdout);
            return document.RootElement.GetProperty("data").Clone();
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

        private string NewModel(string fileName)
        {
            string path = Path.Join(this.WorkingDirectory, fileName);
            Capture(() => NewCommand.Run(new[] { path, "--name", "Test" }));
            return path;
        }
    }
}
