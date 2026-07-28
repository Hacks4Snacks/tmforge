namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Engine;
    using ThreatModelForge.Model;

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

        /// <summary>Two flows the structural key cannot tell apart: same endpoints, same name.</summary>
        private const string DuplicateFlowManifest =
            "{\"name\":\"T\",\"elements\":[" +
            "{\"alias\":\"a\",\"kind\":\"external\",\"name\":\"Client\"}," +
            "{\"alias\":\"b\",\"kind\":\"process\",\"name\":\"Service\"}]," +
            "\"flows\":[{\"from\":\"a\",\"to\":\"b\",\"name\":\"Call\"}," +
            "{\"from\":\"a\",\"to\":\"b\",\"name\":\"Call\"}]}";

        /// <summary>A flow carrying its own alias.</summary>
        private const string AliasedFlowManifest =
            "{\"name\":\"T\",\"elements\":[" +
            "{\"alias\":\"a\",\"kind\":\"external\",\"name\":\"Client\"}," +
            "{\"alias\":\"b\",\"kind\":\"process\",\"name\":\"Service\"}]," +
            "\"flows\":[{\"alias\":\"primary\",\"from\":\"a\",\"to\":\"b\",\"name\":\"Request\"}]}";

        /// <summary>A manifest declaring no aliases at all — every id has to come from a structural key.</summary>
        private const string NoAliasManifest =
            "{\"name\":\"T\",\"boundaries\":[{\"name\":\"Edge\"}]," +
            "\"elements\":[" +
            "{\"kind\":\"external\",\"name\":\"Client\"}," +
            "{\"kind\":\"process\",\"name\":\"Service\"}]," +
            "\"flows\":[{\"from\":\"Client\",\"to\":\"Service\",\"name\":\"Call\"}]}";

        /// <summary>
        /// An alias written to look exactly like the structural key of another element. Both are
        /// author-controlled strings, so the two derivations have to live in separate namespaces.
        /// </summary>
        private const string AliasShapedLikeAStructuralKeyManifest =
            "{\"name\":\"T\",\"elements\":[" +
            "{\"alias\":\"element:Widget\",\"kind\":\"process\",\"name\":\"Aliased\"}," +
            "{\"kind\":\"process\",\"name\":\"Widget\"}]}";

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

        /// <summary>
        /// Applying one manifest twice reproduces every identifier — the page, each component, and each
        /// connector.
        /// </summary>
        /// <remarks>
        /// <c>apply</c> rebuilds the whole model, so any identifier it mints rather than derives moves
        /// on every run. That is not cosmetic: a finding id is
        /// <c>{ruleId}:{diagram}:{target}:{occurrence}</c>, so a fresh page guid alone moves every
        /// finding in the model, orphans the triage recorded against a threat-register key, and makes
        /// a no-op re-apply read as a wholesale rewrite in <c>tmforge diff</c> and in the pull-request
        /// review the Action posts.
        /// </remarks>
        [TestMethod]
        public void ApplyingOneManifestTwiceReproducesEveryIdentifier()
        {
            Manifest manifest = ManifestSupport.Deserialize(SampleManifest)
                ?? throw new InvalidOperationException("the sample manifest must parse");

            Assert.IsTrue(ManifestSupport.Build(manifest, force: false, out ThreatModel first, out _, out string? firstError), firstError);
            Assert.IsTrue(ManifestSupport.Build(manifest, force: false, out ThreatModel second, out _, out string? secondError), secondError);

            DrawingSurfaceModel firstPage = first.DrawingSurfaceList[0];
            DrawingSurfaceModel secondPage = second.DrawingSurfaceList[0];

            Assert.AreEqual(firstPage.Guid, secondPage.Guid, "the page identifier moved between applies");
            CollectionAssert.AreEquivalent(
                firstPage.Borders.Keys.ToList(),
                secondPage.Borders.Keys.ToList(),
                "component identifiers moved between applies");
            CollectionAssert.AreEquivalent(
                firstPage.Lines.Keys.ToList(),
                secondPage.Lines.Keys.ToList(),
                "connector identifiers moved between applies");
        }

        /// <summary>
        /// Two flows between the same pair of elements with the same name still get distinct identifiers,
        /// and the same two on the next apply. Deriving an id from a structural key is only safe if
        /// same-keyed siblings are disambiguated; collapsing them would silently drop a flow.
        /// </summary>
        [TestMethod]
        public void FlowsSharingEndpointsAndNameGetDistinctStableIdentifiers()
        {
            Manifest manifest = ManifestSupport.Deserialize(DuplicateFlowManifest)
                ?? throw new InvalidOperationException("the manifest must parse");

            Assert.IsTrue(ManifestSupport.Build(manifest, force: false, out ThreatModel first, out _, out string? error), error);
            Assert.IsTrue(ManifestSupport.Build(manifest, force: false, out ThreatModel second, out _, out error), error);

            List<Guid> firstFlows = first.DrawingSurfaceList[0].Lines.Keys.ToList();

            Assert.AreEqual(2, firstFlows.Count, "both flows must survive");
            Assert.AreEqual(2, firstFlows.Distinct().Count(), "the two flows collapsed onto one identifier");
            CollectionAssert.AreEquivalent(firstFlows, second.DrawingSurfaceList[0].Lines.Keys.ToList());
        }

        /// <summary>
        /// A flow alias fixes the connector's identity and survives <c>export</c>, so a flow's identity
        /// can be carried deliberately rather than inferred from its endpoints and name — which is what
        /// lets a flow be renamed or re-pointed without moving its id.
        /// </summary>
        [TestMethod]
        public void FlowAliasFixesIdentityAndSurvivesExport()
        {
            Manifest manifest = ManifestSupport.Deserialize(AliasedFlowManifest)
                ?? throw new InvalidOperationException("the manifest must parse");
            Assert.IsTrue(ManifestSupport.Build(manifest, force: false, out ThreatModel model, out _, out string? error), error);

            Guid expected = AuthoringSupport.DeterministicId("primary");
            Assert.IsTrue(
                model.DrawingSurfaceList[0].Lines.ContainsKey(expected),
                "the connector did not take the identifier its alias derives");

            Manifest exported = ManifestSupport.Extract(model);
            Assert.AreEqual("primary", exported.Flows![0].Alias, "export dropped the flow alias");
        }

        /// <summary>
        /// Renaming an aliased flow leaves its identifier alone, which is the point of declaring one:
        /// the structural fallback would move the id, because the name is part of its key.
        /// </summary>
        [TestMethod]
        public void RenamingAnAliasedFlowKeepsItsIdentifier()
        {
            Manifest before = ManifestSupport.Deserialize(AliasedFlowManifest)
                ?? throw new InvalidOperationException("the manifest must parse");
            Manifest after = ManifestSupport.Deserialize(AliasedFlowManifest.Replace("Request", "Renamed", StringComparison.Ordinal))
                ?? throw new InvalidOperationException("the manifest must parse");

            Assert.IsTrue(ManifestSupport.Build(before, force: false, out ThreatModel first, out _, out string? error), error);
            Assert.IsTrue(ManifestSupport.Build(after, force: false, out ThreatModel second, out _, out error), error);

            CollectionAssert.AreEquivalent(
                first.DrawingSurfaceList[0].Lines.Keys.ToList(),
                second.DrawingSurfaceList[0].Lines.Keys.ToList(),
                "renaming an aliased flow moved its identifier");
        }

        /// <summary>A flow may not take an alias an element already holds, or the two would collide.</summary>
        [TestMethod]
        public void FlowAliasCollidingWithAnElementIsRejected()
        {
            Manifest manifest = ManifestSupport.Deserialize(
                AliasedFlowManifest.Replace("\"alias\":\"primary\"", "\"alias\":\"a\"", StringComparison.Ordinal))
                ?? throw new InvalidOperationException("the manifest must parse");

            Assert.IsFalse(ManifestSupport.Build(manifest, force: false, out _, out _, out string? error));
            StringAssert.Contains(error, "Duplicate alias");
        }

        /// <summary>
        /// A manifest that declares no aliases is still reproduced identically. Aliases are optional and
        /// a concise manifest is the documented starting point, so the structural fallback has to hold
        /// on its own — otherwise the guarantee would reach only manifests somebody had already annotated.
        /// </summary>
        [TestMethod]
        public void AManifestWithoutAliasesIsStillReproducedIdentically()
        {
            Manifest manifest = ManifestSupport.Deserialize(NoAliasManifest)
                ?? throw new InvalidOperationException("the manifest must parse");

            Assert.IsTrue(ManifestSupport.Build(manifest, force: false, out ThreatModel first, out _, out string? error), error);
            Assert.IsTrue(ManifestSupport.Build(manifest, force: false, out ThreatModel second, out _, out error), error);

            DrawingSurfaceModel firstPage = first.DrawingSurfaceList[0];
            DrawingSurfaceModel secondPage = second.DrawingSurfaceList[0];

            Assert.AreEqual(3, firstPage.Borders.Count, "the boundary and both elements must be present");
            CollectionAssert.AreEquivalent(
                firstPage.Borders.Keys.ToList(),
                secondPage.Borders.Keys.ToList(),
                "component identifiers moved between applies");
            CollectionAssert.AreEquivalent(
                firstPage.Lines.Keys.ToList(),
                secondPage.Lines.Keys.ToList(),
                "connector identifiers moved between applies");
        }

        /// <summary>
        /// An alias may read exactly like another element's structural key without the two colliding.
        /// Both strings are author-controlled, and the alias and structural allocators use separate
        /// uniqueness sets, so a shared derivation namespace would let the second object overwrite the
        /// first in the diagram and vanish — while the summary still counted it.
        /// </summary>
        [TestMethod]
        public void AnAliasShapedLikeAStructuralKeyDoesNotCollide()
        {
            Manifest manifest = ManifestSupport.Deserialize(AliasShapedLikeAStructuralKeyManifest)
                ?? throw new InvalidOperationException("the manifest must parse");

            Assert.IsTrue(ManifestSupport.Build(manifest, force: false, out ThreatModel model, out _, out string? error), error);

            Assert.AreEqual(2, model.DrawingSurfaceList[0].Borders.Count, "an element was lost to an identifier collision");
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
