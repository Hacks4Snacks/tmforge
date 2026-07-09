namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Engine;

    /// <summary>
    /// Unit tests for the <c>tmforge mcp</c> tool layer: the mapping from agent-friendly parameters
    /// (kind nouns, property maps, file paths) onto the engine and authoring facades. The MCP protocol
    /// wiring itself is exercised by a stdio smoke test; these verify the thin tool adapters.
    /// </summary>
    [TestClass]
    public class McpToolsTest
    {
        /// <summary>Gets or sets the working directory created for each test.</summary>
        private string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>Creates an isolated working directory for the test.</summary>
        [TestInitialize]
        public void Initialize()
        {
            this.WorkingDirectory = Path.Combine(Path.GetTempPath(), "tmforge-mcp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.WorkingDirectory);
        }

        /// <summary>Removes the working directory after the test.</summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(this.WorkingDirectory))
            {
                Directory.Delete(this.WorkingDirectory, recursive: true);
            }
        }

        /// <summary>
        /// Verifies that <c>add</c> resolves the kind noun, marshals the property map, and returns the model.
        /// </summary>
        [TestMethod]
        public void Add_ResolvesKindNounAndStampsProperties()
        {
            AuthoringResultDto result = McpAuthoringTools.Add(
                model: null,
                kind: "process",
                name: "API",
                alias: "api",
                properties: new Dictionary<string, string> { ["AuthenticationScheme"] = "OAuth" });

            Assert.IsTrue(result.Success, result.Error);
            TmForgeElementDto element = result.Model!.Elements!.Single();
            Assert.AreEqual("OAuth", element.Properties["AuthenticationScheme"]);
        }

        /// <summary>
        /// Verifies that <c>add</c> with an unrecognized kind reports an error instead of throwing.
        /// </summary>
        [TestMethod]
        public void Add_UnknownKind_ReturnsError()
        {
            AuthoringResultDto result = McpAuthoringTools.Add(model: null, kind: "widget", name: "X");

            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.Error);
        }

        /// <summary>
        /// Verifies that a model built with <c>apply</c> threads into <c>analyze</c> and produces real findings.
        /// </summary>
        [TestMethod]
        public void Apply_ThenAnalyze_ThreadsModelThroughTools()
        {
            Manifest manifest = new Manifest
            {
                Elements = new List<ManifestElement>
                {
                    new ManifestElement { Alias = "p1", Kind = "process", Name = "Web" },
                    new ManifestElement { Alias = "e1", Kind = "external", Name = "User" },
                },
                Flows = new List<ManifestFlow> { new ManifestFlow { From = "e1", To = "p1", Name = "req" } },
            };

            ApplyResultDto applied = McpAuthoringTools.Apply(manifest, force: false);
            Assert.IsTrue(applied.Success, applied.Error);

            IReadOnlyList<FindingDto> findings = McpModelTools.Analyze(applied.Model!);
            Assert.IsFalse(findings.Any(finding => finding.Id == "engine-error"));
        }

        /// <summary>
        /// Verifies that <c>save</c> writes a model to disk and <c>read</c> loads it back.
        /// </summary>
        [TestMethod]
        public void Save_And_Read_RoundTripAModel()
        {
            AuthoringResultDto added = McpAuthoringTools.Add(model: null, kind: "process", name: "Web", alias: "web");
            string path = Path.Combine(this.WorkingDirectory, "model.tm7");

            McpSaveResult saved = McpModelTools.Save(added.Model!, path);
            Assert.IsTrue(File.Exists(path));
            Assert.AreEqual("tm7", saved.Format);
            Assert.IsTrue(saved.Bytes > 0);

            TmForgeModelDto reread = McpModelTools.Read(path);
            Assert.AreEqual(1, reread.Elements!.Count);
            Assert.AreEqual("Web", reread.Elements![0].Name);
        }

        /// <summary>
        /// Verifies that the grounding tools return their catalogs (schema, rules, stencils, formats).
        /// </summary>
        [TestMethod]
        public void Grounding_Tools_ReturnCatalogs()
        {
            Assert.IsTrue(McpGroundingTools.ManifestSchema().Length > 0);
            Assert.IsTrue(McpGroundingTools.Rules().Count > 0);
            Assert.IsTrue(McpGroundingTools.PropertySchema().Count > 0);
            Assert.IsTrue(McpGroundingTools.Stencils().Count > 0);
            Assert.IsTrue(McpGroundingTools.Formats().Count > 0);
        }

        /// <summary>
        /// Verifies that a property map is marshaled into the <c>KEY=VALUE</c> assignment list.
        /// </summary>
        [TestMethod]
        public void ToAssignments_MarshalsPropertyMap()
        {
            List<string> assignments = McpToolSupport.ToAssignments(new Dictionary<string, string> { ["Protocol"] = "HTTPS", ["Port"] = "443" }).ToList();

            Assert.AreEqual(2, assignments.Count);
            CollectionAssert.Contains(assignments, "Protocol=HTTPS");
            CollectionAssert.Contains(assignments, "Port=443");
        }
    }
}
