namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for the declarative manifest verbs (<c>tmforge apply</c> and <c>tmforge export</c>).
    /// </summary>
    [TestClass]
    public class ApplyExportTest
    {
        private const string SampleManifest =
            "{\"name\":\"T\",\"boundaries\":[{\"alias\":\"TB\",\"name\":\"Edge\"}]," +
            "\"elements\":[" +
            "{\"alias\":\"P1\",\"kind\":\"process\",\"name\":\"Proc\",\"boundary\":\"TB\"}," +
            "{\"alias\":\"DS\",\"kind\":\"store\",\"name\":\"Store\",\"boundary\":\"TB\"}," +
            "{\"alias\":\"EXT\",\"kind\":\"external\",\"name\":\"Client\"}]," +
            "\"flows\":[{\"from\":\"EXT\",\"to\":\"P1\",\"name\":\"call\",\"props\":{\"Protocol\":\"HTTPS\"}}," +
            "{\"from\":\"P1\",\"to\":\"DS\",\"name\":\"write\"}]}";

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
            this.WorkingDirectory = Path.Join(Path.GetTempPath(), "tmforge-manifest-" + Guid.NewGuid().ToString("N"));
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
        /// <c>apply</c> materializes the manifest into a model with the right element/flow/boundary counts.
        /// </summary>
        [TestMethod]
        public void ApplyBuildsModelFromManifest()
        {
            string manifest = this.WriteManifest(SampleManifest);
            string model = Path.ChangeExtension(manifest, ".tm7");

            (int exit, string stdout) = Capture(() => ApplyCommand.Run(new[] { manifest, "--json" }));

            Assert.AreEqual(0, exit);
            using JsonDocument document = JsonDocument.Parse(stdout);
            JsonElement data = document.RootElement.GetProperty("data");
            Assert.AreEqual(1, data.GetProperty("boundaries").GetInt32());
            Assert.AreEqual(3, data.GetProperty("elements").GetInt32());
            Assert.AreEqual(2, data.GetProperty("flows").GetInt32());
            Assert.IsTrue(File.Exists(model));

            JsonElement open = OpenData(model);
            Assert.AreEqual(3, open.GetProperty("componentCount").GetInt32());
            Assert.AreEqual(2, open.GetProperty("connectorCount").GetInt32());
            Assert.AreEqual(1, open.GetProperty("trustBoundaryCount").GetInt32());
        }

        /// <summary>
        /// <c>apply --dry-run</c> validates the manifest but writes nothing.
        /// </summary>
        [TestMethod]
        public void ApplyDryRunWritesNothing()
        {
            string manifest = this.WriteManifest(SampleManifest);
            string model = Path.ChangeExtension(manifest, ".tm7");

            (int exit, string stdout) = Capture(() => ApplyCommand.Run(new[] { manifest, "--dry-run", "--json" }));

            Assert.AreEqual(0, exit);
            Assert.IsFalse(File.Exists(model), "--dry-run must not write the model");
            using JsonDocument document = JsonDocument.Parse(stdout);
            Assert.IsTrue(document.RootElement.GetProperty("data").GetProperty("dryRun").GetBoolean());
        }

        /// <summary>
        /// A manifest with an unresolvable flow endpoint fails and writes no partial model
        /// (transactional apply).
        /// </summary>
        [TestMethod]
        public void ApplyIsTransactionalOnError()
        {
            string bad = "{\"elements\":[{\"alias\":\"P1\",\"kind\":\"process\",\"name\":\"Proc\"}]," +
                "\"flows\":[{\"from\":\"P1\",\"to\":\"NOPE\",\"name\":\"x\"}]}";
            string manifest = this.WriteManifest(bad);
            string model = Path.ChangeExtension(manifest, ".tm7");

            (int exit, _) = Capture(() => ApplyCommand.Run(new[] { manifest, "--json" }));

            Assert.AreEqual(1, exit);
            Assert.IsFalse(File.Exists(model), "a failed apply must not leave a partial model");
        }

        /// <summary>
        /// A manifest that reuses an alias is rejected.
        /// </summary>
        [TestMethod]
        public void ApplyDuplicateAliasFails()
        {
            string dup = "{\"elements\":[" +
                "{\"alias\":\"P1\",\"kind\":\"process\",\"name\":\"A\"}," +
                "{\"alias\":\"P1\",\"kind\":\"process\",\"name\":\"B\"}]}";
            string manifest = this.WriteManifest(dup);

            (int exit, _) = Capture(() => ApplyCommand.Run(new[] { manifest, "--json" }));

            Assert.AreEqual(1, exit);
        }

        /// <summary>
        /// <c>export</c> round-trips a model applied from a manifest back into an equivalent manifest,
        /// preserving aliases, kinds, boundary membership, and properties.
        /// </summary>
        [TestMethod]
        public void ExportRoundTripsManifest()
        {
            string manifest = this.WriteManifest(SampleManifest);
            string model = Path.ChangeExtension(manifest, ".tm7");
            Capture(() => ApplyCommand.Run(new[] { manifest }));

            (int exit, string stdout) = Capture(() => ExportCommand.Run(new[] { model }));

            Assert.AreEqual(0, exit);
            using JsonDocument document = JsonDocument.Parse(stdout);
            JsonElement root = document.RootElement;
            Assert.AreEqual("T", root.GetProperty("name").GetString());
            Assert.AreEqual(1, root.GetProperty("boundaries").GetArrayLength());
            Assert.AreEqual(3, root.GetProperty("elements").GetArrayLength());
            Assert.AreEqual(2, root.GetProperty("flows").GetArrayLength());

            bool foundP1 = false;
            foreach (JsonElement element in root.GetProperty("elements").EnumerateArray()
                .Where(element => element.GetProperty("alias").GetString() == "P1"))
            {
                Assert.AreEqual("process", element.GetProperty("kind").GetString());
                Assert.AreEqual("TB", element.GetProperty("boundary").GetString());
                foundP1 = true;
            }

            Assert.IsTrue(foundP1, "the exported manifest must preserve P1 with its boundary membership");
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

        private string WriteManifest(string json)
        {
            string path = Path.Join(this.WorkingDirectory, "model.json");
            File.WriteAllText(path, json);
            return path;
        }
    }
}
