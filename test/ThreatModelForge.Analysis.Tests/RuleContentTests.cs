namespace ThreatModelForge.Analysis.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for content-based rule loading: the seam that lets hosts without a filesystem (the
    /// HTTP API, the in-browser engine) load exactly the rules the CLI loads from a path. The contract
    /// under test is equivalence — same rules, same pack identity, same content fingerprint — because
    /// that is what makes one custom pack behave identically on every transport.
    /// </summary>
    [TestClass]
    public class RuleContentTests
    {
        private const string Pack =
            "{\"schema\":\"tmforge-rules\",\"version\":2,\"dialect\":\"urn:tmforge:rules:flat-v1\"," +
            "\"pack\":{\"id\":\"corporate\",\"name\":\"Corporate baseline\",\"version\":\"2.1\"}," +
            "\"categories\":[{\"id\":\"privacy\",\"name\":\"Privacy\"}]," +
            "\"elementTypes\":[{\"id\":\"GE.DS\",\"name\":\"Data store\",\"parentId\":\"ROOT\"}]," +
            "\"properties\":[{\"name\":\"Encrypted\",\"allowedValues\":[\"No\",\"At-rest\"],\"elementTypeIds\":[\"GE.DS\"]}]," +
            "\"rules\":[{\"id\":\"CORP-1\",\"severity\":\"error\",\"categoryId\":\"privacy\"," +
            "\"appliesTo\":\"datastore\",\"message\":\"{name} must encrypt data at rest.\"," +
            "\"assert\":{\"property\":\"Encrypted\",\"equals\":\"At-rest\"}}]}";

        /// <summary>Gets or sets the working directory created for each test.</summary>
        private string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>Creates an isolated working directory for the test.</summary>
        [TestInitialize]
        public void Initialize()
        {
            this.WorkingDirectory = Path.Join(Path.GetTempPath(), "tmforge-content-" + Guid.NewGuid().ToString("N"));
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
        /// The same pack loaded from content and from a file produces the same rule identity and the
        /// same content fingerprint, so a model analyzed through the API and through the CLI is
        /// analyzed against provably identical rules.
        /// </summary>
        [TestMethod]
        public void ContentAndFileLoadsAreEquivalent()
        {
            string path = this.WriteSpec(Pack);

            RuleBundle fromFile = DeclarativeRuleProvider.LoadBundle(new[] { path });
            RuleBundle fromContent = DeclarativeRuleProvider.LoadBundle(
                new[] { RuleContent.FromJson("corporate.tmrules.json", Pack) });

            Assert.AreEqual("corporate/CORP-1", fromFile.Rules.Single().ID);
            Assert.AreEqual(fromFile.Rules.Single().ID, fromContent.Rules.Single().ID);
            Assert.AreEqual(fromFile.Packs.Single().Fingerprint, fromContent.Packs.Single().Fingerprint);
            Assert.AreEqual("2.1", fromContent.Packs.Single().Version);
        }

        /// <summary>
        /// Content read from a file byte for byte fingerprints identically to loading that file
        /// directly, which is what lets a host resolve a path once and then stay filesystem-free.
        /// </summary>
        [TestMethod]
        public void ReadContentsPreservesTheFileFingerprint()
        {
            string path = this.WriteSpec(Pack);

            IReadOnlyList<RuleContent> contents = DeclarativeRuleProvider.ReadContents(new[] { path });
            RuleBundle fromFile = DeclarativeRuleProvider.LoadBundle(new[] { path });
            RuleBundle fromContent = DeclarativeRuleProvider.LoadBundle(contents);

            Assert.AreEqual(1, contents.Count);
            Assert.AreEqual(fromFile.Packs.Single().Fingerprint, fromContent.Packs.Single().Fingerprint);
        }

        /// <summary>Malformed content is reported against its logical origin, not silently dropped.</summary>
        [TestMethod]
        public void MalformedContentIsReportedWithItsOrigin()
        {
            List<string> diagnostics = new List<string>();

            RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(
                new[] { RuleContent.FromJson("broken.tmrules.json", "{ not json") },
                diagnostics.Add);

            Assert.AreEqual(0, bundle.Rules.Count);
            Assert.IsTrue(diagnostics.Any(message => message.Contains("broken.tmrules.json", StringComparison.Ordinal)));
        }

        /// <summary>Oversized content is rejected before parsing, the same as an oversized file.</summary>
        [TestMethod]
        public void OversizedContentIsRejected()
        {
            List<string> diagnostics = new List<string>();
            string padded = "{\"rules\":[]," + new string(' ', 9 * 1024 * 1024) + "\"trailing\":0}";

            RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(
                new[] { RuleContent.FromJson("huge.tmrules.json", padded) },
                diagnostics.Add);

            Assert.AreEqual(0, bundle.Rules.Count);
            Assert.IsTrue(diagnostics.Any(message => message.Contains("exceeds the limit", StringComparison.Ordinal)));
        }

        /// <summary>
        /// A UTF-8 byte-order mark decodes back to identical JSON, so a pack authored by an editor that
        /// writes a BOM crosses a text transport without changing meaning.
        /// </summary>
        [TestMethod]
        public void ContentDecodesAByteOrderMark()
        {
            byte[] withBom = new byte[] { 0xEF, 0xBB, 0xBF }
                .Concat(Encoding.UTF8.GetBytes(Pack))
                .ToArray();

            RuleContent content = RuleContent.FromBytes("bom.tmrules.json", withBom);

            Assert.AreEqual(Pack, content.ToJson());
            Assert.AreEqual(withBom.Length, content.Length);
        }

        /// <summary>
        /// Rule sources compose built-in rules with content-supplied packs and report which packs
        /// actually contributed rules, so a host can prove the pack it selected is the pack that ran.
        /// </summary>
        [TestMethod]
        public void RuleSourcesComposeContentWithBuiltInRules()
        {
            RuleSourceOptions options = new RuleSourceOptions(
                null,
                null,
                new[] { RuleContent.FromJson("corporate.tmrules.json", Pack) });

            using RuleSet ruleSet = AnalysisRuleSources.Create(options, out IReadOnlyList<RulePackDefinition> packs);

            Assert.IsTrue(ruleSet.Rules.Any(rule => rule.ID == "corporate/CORP-1"));
            Assert.IsTrue(ruleSet.Rules.Any(rule => rule.ID == "TM1002"), "built-in rules still load");
            Assert.AreEqual("corporate", packs.Single().Id);
            Assert.IsTrue(packs.Single().Fingerprint.StartsWith("sha256:", StringComparison.Ordinal));
        }

        /// <summary>An empty selection loads the built-in rules only and reports no custom packs.</summary>
        [TestMethod]
        public void EmptySelectionLoadsBuiltInRulesOnly()
        {
            using RuleSet ruleSet = AnalysisRuleSources.Create(
                new RuleSourceOptions(),
                out IReadOnlyList<RulePackDefinition> packs);

            Assert.AreEqual(0, packs.Count);
            Assert.IsTrue(ruleSet.Rules.Count > 0);
        }

        private string WriteSpec(string json)
        {
            string path = Path.Join(this.WorkingDirectory, Guid.NewGuid().ToString("N") + ".tmrules.json");
            File.WriteAllText(path, json);
            return path;
        }
    }
}
