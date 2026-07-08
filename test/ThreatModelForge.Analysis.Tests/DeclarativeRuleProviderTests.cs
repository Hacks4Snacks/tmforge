namespace ThreatModelForge.Analysis.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for the <see cref="DeclarativeRuleProvider"/> class.
    /// </summary>
    [TestClass]
    public class DeclarativeRuleProviderTests
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
            this.WorkingDirectory = Path.Combine(Path.GetTempPath(), "tmforge-rules-" + Guid.NewGuid().ToString("N"));
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
        /// A valid spec loads a single rule whose id is surfaced verbatim (the string-id constructor)
        /// and whose severity, pack, STRIDE category, and external references are parsed.
        /// </summary>
        [TestMethod]
        public void LoadsValidRuleWithMetadata()
        {
            string spec =
                "{\"rules\":[{" +
                "\"id\":\"ACME001\",\"pack\":\"acme\",\"severity\":\"error\"," +
                "\"appliesTo\":\"datastore\",\"message\":\"{name} is not encrypted\"," +
                "\"stride\":\"InformationDisclosure\",\"threatReferences\":[\"CWE:311\"]," +
                "\"assert\":{\"property\":\"Encrypted\",\"notAnyOf\":[\"No\"]}}]}";
            string path = this.WriteSpec(spec);

            IReadOnlyList<Rule> rules = DeclarativeRuleProvider.Load(new[] { path });

            Assert.AreEqual(1, rules.Count);
            Rule rule = rules[0];
            Assert.AreEqual("ACME001", rule.ID);
            Assert.AreEqual("acme", rule.Pack);
            Assert.AreEqual(MessageSeverity.Error, rule.Severity);
            Assert.AreEqual(StrideCategory.InformationDisclosure, rule.Stride);
            Assert.AreEqual(1, rule.ThreatReferences.Count);
            Assert.AreEqual("CWE-311", rule.ThreatReferences[0].Id);
        }

        /// <summary>
        /// A rule without an id is skipped and reported through diagnostics.
        /// </summary>
        [TestMethod]
        public void SkipsRuleMissingId()
        {
            string spec = "{\"rules\":[{\"appliesTo\":\"datastore\",\"message\":\"x\",\"assert\":{\"property\":\"Encrypted\",\"present\":true}}]}";
            string path = this.WriteSpec(spec);
            List<string> diagnostics = new List<string>();

            IReadOnlyList<Rule> rules = DeclarativeRuleProvider.Load(new[] { path }, diagnostics.Add);

            Assert.AreEqual(0, rules.Count);
            Assert.IsTrue(diagnostics.Any(message => message.Contains("id")));
        }

        /// <summary>
        /// A rule with an unrecognized <c>appliesTo</c> is skipped and reported.
        /// </summary>
        [TestMethod]
        public void SkipsUnknownAppliesTo()
        {
            string spec = "{\"rules\":[{\"id\":\"X1\",\"appliesTo\":\"widget\",\"message\":\"x\",\"assert\":{\"property\":\"P\",\"present\":true}}]}";
            string path = this.WriteSpec(spec);
            List<string> diagnostics = new List<string>();

            IReadOnlyList<Rule> rules = DeclarativeRuleProvider.Load(new[] { path }, diagnostics.Add);

            Assert.AreEqual(0, rules.Count);
            Assert.IsTrue(diagnostics.Any(message => message.Contains("appliesTo")));
        }

        /// <summary>
        /// Relational facets are only valid on flow rules; using one on a non-flow rule is rejected.
        /// </summary>
        [TestMethod]
        public void SkipsRelationalConditionOnNonFlow()
        {
            string spec = "{\"rules\":[{\"id\":\"X1\",\"appliesTo\":\"datastore\",\"message\":\"x\",\"when\":{\"crossesTrustBoundary\":true}}]}";
            string path = this.WriteSpec(spec);
            List<string> diagnostics = new List<string>();

            IReadOnlyList<Rule> rules = DeclarativeRuleProvider.Load(new[] { path }, diagnostics.Add);

            Assert.AreEqual(0, rules.Count);
            Assert.IsTrue(diagnostics.Any(message => message.Contains("flow")));
        }

        /// <summary>
        /// A rule with neither a guard nor a requirement is rejected.
        /// </summary>
        [TestMethod]
        public void SkipsRuleWithoutWhenOrAssert()
        {
            string spec = "{\"rules\":[{\"id\":\"X1\",\"appliesTo\":\"process\",\"message\":\"x\"}]}";
            string path = this.WriteSpec(spec);
            List<string> diagnostics = new List<string>();

            IReadOnlyList<Rule> rules = DeclarativeRuleProvider.Load(new[] { path }, diagnostics.Add);

            Assert.AreEqual(0, rules.Count);
            Assert.IsTrue(diagnostics.Any(message => message.Contains("when") || message.Contains("assert")));
        }

        /// <summary>
        /// A malformed spec file is skipped with a diagnostic rather than throwing.
        /// </summary>
        [TestMethod]
        public void SkipsMalformedJson()
        {
            string path = this.WriteSpec("{ this is not valid json");
            List<string> diagnostics = new List<string>();

            IReadOnlyList<Rule> rules = DeclarativeRuleProvider.Load(new[] { path }, diagnostics.Add);

            Assert.AreEqual(0, rules.Count);
            Assert.AreEqual(1, diagnostics.Count);
        }

        private string WriteSpec(string json)
        {
            string path = Path.Combine(this.WorkingDirectory, Guid.NewGuid().ToString("N") + ".tmrules.json");
            File.WriteAllText(path, json);
            return path;
        }
    }
}
