namespace ThreatModelForge.Api.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Engine;

    /// <summary>
    /// Tests for reading a stored <c>tmforge-analysis</c> document and for the optional mapping to an
    /// engagement's own threat catalogue.
    /// </summary>
    [TestClass]
    public class AnalysisDocumentReaderTest
    {
        /// <summary>A current document round-trips through the writer and the reader.</summary>
        [TestMethod]
        public void CurrentDocumentIsRead()
        {
            string json = JsonSerializer.Serialize(new AnalysisDocumentDto
            {
                Model = new AnalysisIdentityDto { Name = "m", Fingerprint = "sha256:aa" },
                Analyzer = new AnalysisIdentityDto { Name = "a", Version = "1.0", Fingerprint = "sha256:bb" },
            });

            Assert.IsTrue(AnalysisDocumentReader.TryRead(json, null, out AnalysisDocumentDto? document, out _));
            Assert.AreEqual(1, document!.Version);
        }

        /// <summary>
        /// A caller can pin the schema it was written against, so a document of another version is
        /// refused rather than read on assumptions that no longer hold.
        /// </summary>
        [TestMethod]
        public void PinnedVersionMismatchIsRefused()
        {
            string json = JsonSerializer.Serialize(new AnalysisDocumentDto
            {
                Model = new AnalysisIdentityDto { Fingerprint = "sha256:aa" },
                Analyzer = new AnalysisIdentityDto { Fingerprint = "sha256:bb" },
            });

            Assert.IsFalse(AnalysisDocumentReader.TryRead(json, 2, out _, out IReadOnlyList<string> problems));
            Assert.IsTrue(problems.Any(problem => problem.Contains("Expected analysis schema version 2")));
        }

        /// <summary>
        /// A document from a newer build is refused. Reading it with today's rules would silently
        /// misinterpret fields whose meaning has changed, which is worse than declining.
        /// </summary>
        [TestMethod]
        public void NewerDocumentIsRefusedWithAnUpgradeHint()
        {
            string json = "{\"schema\":\"tmforge-analysis\",\"version\":99}";

            Assert.IsFalse(AnalysisDocumentReader.TryRead(json, null, out _, out IReadOnlyList<string> problems));
            Assert.IsTrue(problems.Any(problem => problem.Contains("newer than this build reads")));
        }

        /// <summary>An older document says so plainly rather than failing obscurely.</summary>
        [TestMethod]
        public void OlderDocumentReportsThatNoMigrationExists()
        {
            string json = "{\"schema\":\"tmforge-analysis\",\"version\":0}";

            Assert.IsFalse(AnalysisDocumentReader.TryRead(json, null, out _, out IReadOnlyList<string> problems));
            Assert.IsTrue(problems.Any(problem => problem.Contains("no migration to version 1")));
        }

        /// <summary>
        /// The findings JSON written beside the document is a different artifact, and pointing the
        /// reader at it should say so rather than produce an empty analysis.
        /// </summary>
        [TestMethod]
        public void ADifferentArtifactIsRefused()
        {
            Assert.IsFalse(AnalysisDocumentReader.TryRead(
                "{\"sourcePath\":\"model.tm7\",\"ruleReports\":[]}",
                null,
                out _,
                out IReadOnlyList<string> problems));
            Assert.IsTrue(problems.Any(problem => problem.Contains("is not a tmforge-analysis document")));
        }

        /// <summary>Malformed input is reported as malformed.</summary>
        [TestMethod]
        public void MalformedJsonIsRefused()
        {
            Assert.IsFalse(AnalysisDocumentReader.TryRead("{ not json", null, out _, out IReadOnlyList<string> problems));
            Assert.IsTrue(problems.Any(problem => problem.Contains("not valid JSON")));
        }

        /// <summary>A mapping file is read and resolves the rules it declares.</summary>
        [TestMethod]
        public void TaxonomyMapResolvesDeclaredRules()
        {
            const string json =
                "{\"schema\":\"tmforge-taxonomy\",\"version\":1,\"rules\":{" +
                "\"TM1013\":[\"ACME-T-017\",\"ACME-T-018\"]}}";

            Assert.IsTrue(AnalysisTaxonomyMap.TryRead(json, out AnalysisTaxonomyMap? map, out _));
            CollectionAssert.AreEqual(new[] { "ACME-T-017", "ACME-T-018" }, map!.For("TM1013").ToArray());
        }

        /// <summary>
        /// An unmapped rule gets nothing. This is the property that matters: a guessed mapping is worse
        /// than an absent one, because a reader cannot tell it was guessed.
        /// </summary>
        [TestMethod]
        public void UnmappedRuleGetsNoCanonicalIds()
        {
            const string json = "{\"schema\":\"tmforge-taxonomy\",\"version\":1,\"rules\":{\"TM1013\":[\"ACME-T-017\"]}}";

            Assert.IsTrue(AnalysisTaxonomyMap.TryRead(json, out AnalysisTaxonomyMap? map, out _));
            Assert.AreEqual(0, map!.For("TM1021").Count);
            Assert.AreEqual(0, map.For(null).Count);
            Assert.AreEqual(0, map.For("corporate/CORP-1").Count);
        }

        /// <summary>A mapping of another schema or version is refused rather than half-applied.</summary>
        /// <param name="json">The mapping document under test.</param>
        [TestMethod]
        [DataRow("{\"schema\":\"tmforge-rules\",\"version\":1}")]
        [DataRow("{\"schema\":\"tmforge-taxonomy\",\"version\":7}")]
        public void UnsupportedTaxonomyMapIsRefused(string json)
        {
            Assert.IsFalse(AnalysisTaxonomyMap.TryRead(json, out _, out IReadOnlyList<string> problems));
            Assert.IsTrue(problems.Count > 0);
        }
    }
}
