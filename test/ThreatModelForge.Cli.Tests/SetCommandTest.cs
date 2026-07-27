namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Engine;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for the <c>tmforge set</c> command (setting name/properties on existing elements
    /// and flows to resolve linter findings).
    /// </summary>
    [TestClass]
    public class SetCommandTest
    {
        /// <summary>
        /// Gets or sets the working directory created for each test.
        /// </summary>
        private string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Gets the path of the model file created by <see cref="NewModel"/>.
        /// </summary>
        private string ModelPath => Path.Join(this.WorkingDirectory, "model.tm7");

        /// <summary>
        /// Creates an isolated working directory for the test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            this.WorkingDirectory = Path.Join(Path.GetTempPath(), "tmforge-set-" + Guid.NewGuid().ToString("N"));
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
        /// Verifies that <c>set --property</c> annotates an existing element and reports it back.
        /// </summary>
        [TestMethod]
        public void SetAppliesPropertiesToElement()
        {
            string path = this.NewModel();
            string id = this.AddElement("process", "API");

            (int exit, string stdout) = Capture(() => SetCommand.Run(new[] { path, "--id", id, "--property", "AuthenticationScheme=OAuth", "--json" }));

            Assert.AreEqual(0, exit);
            JsonElement data = JsonDocument.Parse(stdout).RootElement.GetProperty("data");
            Assert.AreEqual("OAuth", data.GetProperty("properties").GetProperty("AuthenticationScheme").GetString());
            Assert.AreEqual("OAuth", this.LoadProperties(Guid.Parse(id))["AuthenticationScheme"]);
        }

        /// <summary>
        /// Verifies that <c>set --property</c> annotates an existing flow (connector).
        /// </summary>
        [TestMethod]
        public void SetAppliesPropertiesToFlow()
        {
            string path = this.NewModel();
            string source = this.AddElement("external", "Browser");
            string target = this.AddElement("process", "API");
            string flow = this.Connect(source, target);

            (int exit, _) = Capture(() => SetCommand.Run(new[] { path, "--id", flow, "--property", "Protocol=HTTPS" }));

            Assert.AreEqual(0, exit);
            Assert.AreEqual("HTTPS", this.LoadProperties(Guid.Parse(flow))["Protocol"]);
        }

        /// <summary>
        /// Verifies that <c>set --name</c> renames an existing element.
        /// </summary>
        [TestMethod]
        public void SetRenamesElement()
        {
            string path = this.NewModel();
            string id = this.AddElement("process", "Original");

            (int exit, _) = Capture(() => SetCommand.Run(new[] { path, "--id", id, "--name", "Renamed" }));

            Assert.AreEqual(0, exit);
            (ThreatModel model, _) = CliModelLoader.Load(path);
            DrawingSurfaceModel? diagram = AuthoringSupport.FirstDiagram(model);
            Assert.IsNotNull(diagram);
            Entity? element = DiagramEditor.FindElement(diagram, Guid.Parse(id));
            Assert.IsNotNull(element);
            Assert.AreEqual("Renamed", DiagramElementHelper.GetName(element));
        }

        /// <summary>
        /// Verifies that setting an unknown identifier is reported as an error.
        /// </summary>
        [TestMethod]
        public void SetUnknownIdReturnsError()
        {
            string path = this.NewModel();

            (int exit, _) = Capture(() => SetCommand.Run(new[] { path, "--id", Guid.NewGuid().ToString(), "--property", "X=Y" }));

            Assert.AreEqual(1, exit);
        }

        /// <summary>
        /// Verifies that supplying neither a name nor a property is reported as an error.
        /// </summary>
        [TestMethod]
        public void SetNothingToSetReturnsError()
        {
            string path = this.NewModel();
            string id = this.AddElement("process", "API");

            (int exit, _) = Capture(() => SetCommand.Run(new[] { path, "--id", id }));

            Assert.AreEqual(1, exit);
        }

        /// <summary>
        /// The regression this guards: saving a <c>.tm7</c> converts a schema-backed property into the
        /// tool's typed representation, and a later <c>set</c> has to move that typed value. Writing a
        /// custom attribute beside it instead reports success, changes nothing a reader will see, and
        /// leaves the element carrying one more property for every edit.
        /// </summary>
        [TestMethod]
        public void SetOverwritesAPropertyTheFileHasAlreadyTyped()
        {
            string path = this.NewModel();
            string id = this.AddElement("store", "Ledger");
            Guid element = Guid.Parse(id);

            Capture(() => SetCommand.Run(new[] { path, "--id", id, "--property", "Encrypted=At-rest" }));
            Assert.AreEqual("At-rest", this.LoadProperties(element)["Encrypted"]);

            (int exit, _) = Capture(() => SetCommand.Run(new[] { path, "--id", id, "--property", "Encrypted=No" }));

            Assert.AreEqual(0, exit);
            Assert.AreEqual("No", this.LoadProperties(element)["Encrypted"]);
            Assert.AreEqual(
                1,
                this.TypedPropertyCount(element, "Encrypted"),
                "the element should carry one Encrypted property, not one per edit");
        }

        /// <summary>
        /// Verifies that repeated edits keep converging on a single property. A duplicate is not only
        /// a stale read: the tool refuses a model that carries two properties with the same identity.
        /// </summary>
        [TestMethod]
        public void RepeatedSetsDoNotAccumulateProperties()
        {
            string path = this.NewModel();
            string id = this.AddElement("store", "Ledger");
            Guid element = Guid.Parse(id);

            foreach (string value in new[] { "At-rest", "No", "TDE", "Platform" })
            {
                Assert.AreEqual(
                    0,
                    Capture(() => SetCommand.Run(new[] { path, "--id", id, "--property", "Encrypted=" + value })).Exit);
            }

            Assert.AreEqual("Platform", this.LoadProperties(element)["Encrypted"]);
            Assert.AreEqual(1, this.TypedPropertyCount(element, "Encrypted"));
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

        private string NewModel()
        {
            Capture(() => NewCommand.Run(new[] { this.ModelPath, "--name", "Set Test" }));
            return this.ModelPath;
        }

        private string AddElement(string kind, string name)
        {
            (int exit, string stdout) = Capture(() => AddCommand.Run(new[] { kind, this.ModelPath, "--name", name, "--json" }));
            Assert.AreEqual(0, exit);
            return JsonDocument.Parse(stdout).RootElement.GetProperty("data").GetProperty("id").GetGuid().ToString();
        }

        private string Connect(string source, string target)
        {
            (int exit, string stdout) = Capture(() => ConnectCommand.Run(new[] { this.ModelPath, "--source", source, "--target", target, "--json" }));
            Assert.AreEqual(0, exit);
            return JsonDocument.Parse(stdout).RootElement.GetProperty("data").GetProperty("id").GetGuid().ToString();
        }

        private IReadOnlyDictionary<string, string> LoadProperties(Guid id)
        {
            (ThreatModel model, _) = CliModelLoader.Load(this.ModelPath);
            DrawingSurfaceModel? diagram = AuthoringSupport.FirstDiagram(model);
            Assert.IsNotNull(diagram);
            Entity? element = DiagramEditor.FindElement(diagram, id);
            Assert.IsNotNull(element);
            return DiagramElementHelper.GetCustomProperties(element);
        }

        /// <summary>
        /// Counts how many typed (list) properties the saved model records under one display name.
        /// </summary>
        /// <param name="id">The element id.</param>
        /// <param name="name">The property's display name.</param>
        /// <returns>The number of typed properties carrying that name.</returns>
        private int TypedPropertyCount(Guid id, string name)
        {
            (ThreatModel model, _) = CliModelLoader.Load(this.ModelPath);
            DrawingSurfaceModel? diagram = AuthoringSupport.FirstDiagram(model);
            Assert.IsNotNull(diagram);
            Entity? element = DiagramEditor.FindElement(diagram, id);
            Assert.IsNotNull(element);
            return element!.Properties.OfType<ListDisplayAttribute>()
                .Count(list => string.Equals(list.DisplayName, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
