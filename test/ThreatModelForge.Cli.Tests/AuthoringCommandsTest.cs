namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for the imperative authoring commands (<c>new</c>, <c>add</c>, <c>connect</c>,
    /// <c>remove</c>, <c>rename</c>). See ADR 0015.
    /// </summary>
    [TestClass]
    public class AuthoringCommandsTest
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
            this.WorkingDirectory = Path.Combine(Path.GetTempPath(), "tmforge-authoring-" + Guid.NewGuid().ToString("N"));
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
        /// Verifies that <c>new</c> creates a model file whose name and (empty) contents round-trip.
        /// </summary>
        [TestMethod]
        public void NewCreatesModelWithName()
        {
            string path = this.ModelPath("model.tm7");

            (int exit, _) = Capture(() => NewCommand.Run(new[] { path, "--name", "Demo Model" }));

            Assert.AreEqual(0, exit);
            Assert.IsTrue(File.Exists(path));
            JsonElement data = OpenData(path);
            Assert.AreEqual("Demo Model", data.GetProperty("name").GetString());
            Assert.AreEqual(0, data.GetProperty("componentCount").GetInt32());
        }

        /// <summary>
        /// Verifies that <c>new</c> refuses to overwrite an existing file.
        /// </summary>
        [TestMethod]
        public void NewRefusesToOverwriteExistingFile()
        {
            string path = this.ModelPath("model.tm7");
            Capture(() => NewCommand.Run(new[] { path }));

            (int exit, _) = Capture(() => NewCommand.Run(new[] { path }));

            Assert.AreEqual(1, exit);
        }

        /// <summary>
        /// Verifies that <c>add</c> adds a component and reports its identifier.
        /// </summary>
        [TestMethod]
        public void AddProcessIncreasesComponentCount()
        {
            string path = this.NewModel();

            string id = AddElement("process", path, "Web API");

            Assert.IsTrue(Guid.TryParse(id, out _));
            Assert.AreEqual(1, OpenData(path).GetProperty("componentCount").GetInt32());
        }

        /// <summary>
        /// Verifies that an unrecognized element kind is reported as an error.
        /// </summary>
        [TestMethod]
        public void AddUnknownKindReturnsError()
        {
            string path = this.NewModel();

            (int exit, _) = Capture(() => AddCommand.Run(new[] { "widget", path }));

            Assert.AreEqual(1, exit);
        }

        /// <summary>
        /// Verifies that <c>add --stencil</c> maps to the stencil's base primitive, defaults the name
        /// to the stencil label, and stamps the stencil identity plus its preset default properties.
        /// </summary>
        [TestMethod]
        public void AddStencilStampsIdentityAndDefaults()
        {
            string path = this.NewModel();

            (int exit, string stdout) = Capture(() => AddCommand.Run(new[] { "--stencil", "azure-key-vault", path, "--json" }));

            Assert.AreEqual(0, exit);
            using JsonDocument document = JsonDocument.Parse(stdout);
            JsonElement data = document.RootElement.GetProperty("data");
            Assert.AreEqual("DataStore", data.GetProperty("kind").GetString());
            Assert.AreEqual("azure-key-vault", data.GetProperty("stencil").GetString());
            Assert.AreEqual("Azure Key Vault", data.GetProperty("name").GetString());

            Guid id = data.GetProperty("id").GetGuid();
            (ThreatModel model, _) = CliModelLoader.Load(path);
            DrawingSurfaceModel? diagram = AuthoringSupport.FirstDiagram(model);
            Assert.IsNotNull(diagram);
            Entity? element = DiagramEditor.FindElement(diagram, id);
            Assert.IsNotNull(element);
            IReadOnlyDictionary<string, string> properties = DiagramElementHelper.GetCustomProperties(element);
            Assert.AreEqual("azure-key-vault", properties["StencilType"]);
            Assert.AreEqual("Yes", properties["StoresCredentials"]);
            Assert.AreEqual("Yes", properties["Encrypted"]);
        }

        /// <summary>
        /// Verifies that an unknown stencil id is reported as an error.
        /// </summary>
        [TestMethod]
        public void AddUnknownStencilReturnsError()
        {
            string path = this.NewModel();

            (int exit, _) = Capture(() => AddCommand.Run(new[] { "--stencil", "does-not-exist", path }));

            Assert.AreEqual(1, exit);
        }

        /// <summary>
        /// Verifies that <c>add --property</c> stamps a custom property that analysis rules read.
        /// </summary>
        [TestMethod]
        public void AddAppliesProperties()
        {
            string path = this.NewModel();

            (int exit, string stdout) = Capture(() => AddCommand.Run(new[] { "process", path, "--name", "API", "--property", "AuthenticationScheme=OAuth", "--json" }));

            Assert.AreEqual(0, exit);
            Guid id = JsonDocument.Parse(stdout).RootElement.GetProperty("data").GetProperty("id").GetGuid();
            IReadOnlyDictionary<string, string> properties = LoadProperties(path, id);
            Assert.AreEqual("OAuth", properties["AuthenticationScheme"]);
        }

        /// <summary>
        /// Verifies that a malformed <c>--property</c> (missing <c>=</c>) is reported as an error.
        /// </summary>
        [TestMethod]
        public void AddMalformedPropertyReturnsError()
        {
            string path = this.NewModel();

            (int exit, _) = Capture(() => AddCommand.Run(new[] { "process", path, "--property", "novalue" }));

            Assert.AreEqual(1, exit);
        }

        /// <summary>
        /// Verifies that <c>add boundary --width/--height</c> sizes the boundary so it can enclose.
        /// </summary>
        [TestMethod]
        public void AddBoundaryUsesWidthAndHeight()
        {
            string path = this.NewModel();

            (int exit, string stdout) = Capture(() => AddCommand.Run(new[] { "boundary", path, "--name", "Zone", "--left", "10", "--top", "10", "--width", "320", "--height", "240", "--json" }));

            Assert.AreEqual(0, exit);
            Guid id = JsonDocument.Parse(stdout).RootElement.GetProperty("data").GetProperty("id").GetGuid();
            (ThreatModel model, _) = CliModelLoader.Load(path);
            DrawingSurfaceModel? diagram = AuthoringSupport.FirstDiagram(model);
            Assert.IsNotNull(diagram);
            DrawingElement? boundary = DiagramEditor.FindElement(diagram, id) as DrawingElement;
            Assert.IsNotNull(boundary);
            Assert.AreEqual(320, boundary.Width);
            Assert.AreEqual(240, boundary.Height);
        }

        /// <summary>
        /// Verifies that <c>connect --property</c> stamps flow properties that analysis rules read.
        /// </summary>
        [TestMethod]
        public void ConnectAppliesProperties()
        {
            string path = this.NewModel();
            string source = AddElement("external", path, "Browser");
            string target = AddElement("process", path, "Web API");

            (int exit, string stdout) = Capture(() => ConnectCommand.Run(new[] { path, "--source", source, "--target", target, "--property", "Protocol=HTTPS", "--property", "Port=443", "--json" }));

            Assert.AreEqual(0, exit);
            Guid id = JsonDocument.Parse(stdout).RootElement.GetProperty("data").GetProperty("id").GetGuid();
            IReadOnlyDictionary<string, string> properties = LoadProperties(path, id);
            Assert.AreEqual("HTTPS", properties["Protocol"]);
            Assert.AreEqual("443", properties["Port"]);
        }

        /// <summary>
        /// Verifies that <c>connect --property</c> rejects a value outside the schema enum.
        /// </summary>
        [TestMethod]
        public void ConnectRejectsInvalidPropertyValue()
        {
            string path = this.NewModel();
            string source = AddElement("external", path, "Browser");
            string target = AddElement("process", path, "Web API");

            (int exit, _) = Capture(() => ConnectCommand.Run(new[] { path, "--source", source, "--target", target, "--property", "Protocol=SMOKE" }));

            Assert.AreEqual(1, exit);
        }

        /// <summary>
        /// Verifies that <c>--force</c> stores a value outside the schema enum.
        /// </summary>
        [TestMethod]
        public void ConnectForceStoresInvalidPropertyValue()
        {
            string path = this.NewModel();
            string source = AddElement("external", path, "Browser");
            string target = AddElement("process", path, "Web API");

            (int exit, string stdout) = Capture(() => ConnectCommand.Run(new[] { path, "--source", source, "--target", target, "--property", "Protocol=SMOKE", "--force", "--json" }));

            Assert.AreEqual(0, exit);
            Guid id = JsonDocument.Parse(stdout).RootElement.GetProperty("data").GetProperty("id").GetGuid();
            Assert.AreEqual("SMOKE", LoadProperties(path, id)["Protocol"]);
        }

        /// <summary>
        /// Verifies that <c>set --property</c> rejects a property that is not in the schema.
        /// </summary>
        [TestMethod]
        public void SetRejectsUnknownProperty()
        {
            string path = this.NewModel();
            string id = AddElement("process", path, "API");

            (int exit, _) = Capture(() => SetCommand.Run(new[] { path, "--id", id, "--property", "Teleport=Yes" }));

            Assert.AreEqual(1, exit);
        }

        /// <summary>
        /// Verifies that a matched enum value is canonicalized to the schema's casing.
        /// </summary>
        [TestMethod]
        public void SetCanonicalizesEnumCasing()
        {
            string path = this.NewModel();
            string id = AddElement("process", path, "API");

            (int exit, _) = Capture(() => SetCommand.Run(new[] { path, "--id", id, "--property", "AuthenticationScheme=oauth" }));

            Assert.AreEqual(0, exit);
            Assert.AreEqual("OAuth", LoadProperties(path, Guid.Parse(id))["AuthenticationScheme"]);
        }

        /// <summary>
        /// Verifies that <c>connect</c> adds a data flow between two elements.
        /// </summary>
        [TestMethod]
        public void ConnectAddsFlow()
        {
            string path = this.NewModel();
            string source = AddElement("process", path, "Web API");
            string target = AddElement("store", path, "Database");

            (int exit, _) = Capture(() => ConnectCommand.Run(new[] { path, "--source", source, "--target", target, "--name", "query" }));

            Assert.AreEqual(0, exit);
            Assert.AreEqual(1, OpenData(path).GetProperty("connectorCount").GetInt32());
        }

        /// <summary>
        /// Verifies that <c>rename</c> changes an element's display name.
        /// </summary>
        [TestMethod]
        public void RenameUpdatesName()
        {
            string path = this.NewModel();
            string id = AddElement("process", path, "Original");

            (int exit, _) = Capture(() => RenameCommand.Run(new[] { path, "--id", id, "--name", "Renamed" }));

            Assert.AreEqual(0, exit);
            (int listExit, string listOut) = Capture(() => ListCommand.Run(new[] { "components", "--json", path }));
            Assert.AreEqual(0, listExit);
            using JsonDocument document = JsonDocument.Parse(listOut);
            JsonElement items = document.RootElement.GetProperty("data").GetProperty("items");
            Assert.AreEqual("Renamed", items[0].GetProperty("name").GetString());
        }

        /// <summary>
        /// Verifies that removing a component also removes its connected data flows.
        /// </summary>
        [TestMethod]
        public void RemoveCascadesToConnectedFlows()
        {
            string path = this.NewModel();
            string source = AddElement("process", path, "Web API");
            string target = AddElement("store", path, "Database");
            Capture(() => ConnectCommand.Run(new[] { path, "--source", source, "--target", target }));

            (int exit, string stdout) = Capture(() => RemoveCommand.Run(new[] { path, "--id", target, "--json" }));

            Assert.AreEqual(0, exit);
            using JsonDocument document = JsonDocument.Parse(stdout);
            Assert.AreEqual(2, document.RootElement.GetProperty("data").GetProperty("removed").GetArrayLength());
            JsonElement data = OpenData(path);
            Assert.AreEqual(1, data.GetProperty("componentCount").GetInt32());
            Assert.AreEqual(0, data.GetProperty("connectorCount").GetInt32());
        }

        /// <summary>
        /// Verifies that removing an unknown identifier is reported as an error.
        /// </summary>
        [TestMethod]
        public void RemoveUnknownElementReturnsError()
        {
            string path = this.NewModel();

            (int exit, _) = Capture(() => RemoveCommand.Run(new[] { path, "--id", Guid.NewGuid().ToString() }));

            Assert.AreEqual(1, exit);
        }

        private static string AddElement(string kind, string path, string name)
        {
            (int exit, string stdout) = Capture(() => AddCommand.Run(new[] { kind, path, "--name", name, "--json" }));
            Assert.AreEqual(0, exit);
            using JsonDocument document = JsonDocument.Parse(stdout);
            string? id = document.RootElement.GetProperty("data").GetProperty("id").GetString();
            return id ?? string.Empty;
        }

        private static JsonElement OpenData(string path)
        {
            (int exit, string stdout) = Capture(() => OpenCommand.Run(new[] { "--json", path }));
            Assert.AreEqual(0, exit);
            using JsonDocument document = JsonDocument.Parse(stdout);
            return document.RootElement.GetProperty("data").Clone();
        }

        private static IReadOnlyDictionary<string, string> LoadProperties(string path, Guid id)
        {
            (ThreatModel model, _) = CliModelLoader.Load(path);
            DrawingSurfaceModel? diagram = AuthoringSupport.FirstDiagram(model);
            Assert.IsNotNull(diagram);
            Entity? element = DiagramEditor.FindElement(diagram, id);
            Assert.IsNotNull(element);
            return DiagramElementHelper.GetCustomProperties(element);
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
            string path = this.ModelPath("model.tm7");
            Capture(() => NewCommand.Run(new[] { path, "--name", "Test" }));
            return path;
        }

        private string ModelPath(string fileName)
        {
            return System.IO.Path.Combine(this.WorkingDirectory, fileName);
        }
    }
}
