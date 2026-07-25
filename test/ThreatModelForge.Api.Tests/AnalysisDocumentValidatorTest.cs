namespace ThreatModelForge.Api.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Engine;

    /// <summary>
    /// Unit tests for the <see cref="AnalysisDocumentValidator"/> class.
    /// </summary>
    /// <remarks>
    /// Stored evidence that contradicts itself is worse than none, because a consumer will act on it.
    /// These pin the cases the validator has to catch rather than pass through.
    /// </remarks>
    [TestClass]
    public class AnalysisDocumentValidatorTest
    {
        /// <summary>A well-formed document reports nothing.</summary>
        [TestMethod]
        public void CoherentDocumentHasNoProblems()
        {
            Assert.AreEqual(0, AnalysisDocumentValidator.Validate(Document(Hygiene("TM1003:model:model:0"))).Count);
        }

        /// <summary>An unreadable schema version is refused rather than guessed at.</summary>
        [TestMethod]
        public void UnknownSchemaVersionIsRejected()
        {
            AnalysisDocumentDto document = new AnalysisDocumentDto
            {
                Version = 99,
                Model = new AnalysisIdentityDto { Fingerprint = "sha256:aa" },
                Analyzer = new AnalysisIdentityDto { Fingerprint = "sha256:bb" },
            };

            Assert.IsTrue(Problems(document).Any(problem => problem.Contains("Unsupported analysis schema version 99")));
        }

        /// <summary>A document with no fingerprints cannot be checked against anything.</summary>
        [TestMethod]
        public void MissingFingerprintsAreRejected()
        {
            IReadOnlyList<string> problems = AnalysisDocumentValidator.Validate(new AnalysisDocumentDto());

            Assert.IsTrue(problems.Any(problem => problem.Contains("model fingerprint")));
            Assert.IsTrue(problems.Any(problem => problem.Contains("analyzer fingerprint")));
        }

        /// <summary>Two findings sharing an id would reconcile onto one row.</summary>
        [TestMethod]
        public void DuplicateFindingIdIsRejected()
        {
            AnalysisDocumentDto document = Document(
                Hygiene("TM1003:model:model:0"),
                Hygiene("TM1003:model:model:0"));

            Assert.IsTrue(Problems(document).Any(problem => problem.Contains("repeats the id")));
        }

        /// <summary>A finding with no disposition would silently drop out of a reconciliation.</summary>
        [TestMethod]
        public void MissingDispositionIsRejected()
        {
            AnalysisDocumentDto document = Document(new AnalysisFindingDto
            {
                Id = "TM1003:model:model:0",
                RuleId = "TM1003",
                Disposition = string.Empty,
            });

            Assert.IsTrue(Problems(document).Any(problem => problem.Contains("has no disposition")));
        }

        /// <summary>An undefined disposition is a contract violation, not an extension point.</summary>
        [TestMethod]
        public void UnknownDispositionIsRejected()
        {
            AnalysisDocumentDto document = Document(new AnalysisFindingDto
            {
                Id = "TM1003:model:model:0",
                RuleId = "TM1003",
                Disposition = "probably-fine",
            });

            Assert.IsTrue(Problems(document).Any(problem => problem.Contains("unknown disposition 'probably-fine'")));
        }

        /// <summary>A threat-bearing finding with no threat link cannot be joined to the register.</summary>
        [TestMethod]
        public void ThreatBearingFindingWithoutThreatIsRejected()
        {
            AnalysisDocumentDto document = Document(new AnalysisFindingDto
            {
                Id = "TM1013:model:e1:0",
                RuleId = "TM1013",
                Disposition = FindingDispositions.Accepted,
            });

            Assert.IsTrue(Problems(document).Any(problem => problem.Contains("links to no threat")));
        }

        /// <summary>Hygiene that claims a threat is contradicting itself.</summary>
        [TestMethod]
        public void HygieneFindingWithThreatIsRejected()
        {
            AnalysisDocumentDto document = Document(new AnalysisFindingDto
            {
                Id = "TM1003:model:model:0",
                RuleId = "TM1003",
                Disposition = FindingDispositions.Hygiene,
                ThreatId = "abc:TM1003",
            });

            Assert.IsTrue(Problems(document).Any(problem => problem.Contains("yet links to threat")));
        }

        /// <summary>
        /// A finding from a pack the document does not list is dangling: the evidence claims a rule ran
        /// that its own provenance cannot account for.
        /// </summary>
        [TestMethod]
        public void FindingFromAnUnlistedPackIsRejected()
        {
            AnalysisDocumentDto document = Document(new AnalysisFindingDto
            {
                Id = "corporate/CORP-1:model:s1:0",
                RuleId = "corporate/CORP-1",
                Disposition = FindingDispositions.Hygiene,
            });

            Assert.IsTrue(Problems(document).Any(problem => problem.Contains("is not listed in rulePacks")));
        }

        /// <summary>A finding from a listed pack resolves.</summary>
        [TestMethod]
        public void FindingFromAListedPackIsAccepted()
        {
            AnalysisDocumentDto document = new AnalysisDocumentDto
            {
                Model = new AnalysisIdentityDto { Fingerprint = "sha256:aa" },
                Analyzer = new AnalysisIdentityDto { Fingerprint = "sha256:bb" },
                RulePacks = new[] { new RulePackInfoDto { Id = "corporate" } },
                Findings = new[]
                {
                    new AnalysisFindingDto
                    {
                        Id = "corporate/CORP-1:model:s1:0",
                        RuleId = "corporate/CORP-1",
                        Disposition = FindingDispositions.Hygiene,
                    },
                },
            };

            Assert.AreEqual(0, AnalysisDocumentValidator.Validate(document).Count);
        }

        private static IReadOnlyList<string> Problems(AnalysisDocumentDto document) =>
            AnalysisDocumentValidator.Validate(document);

        private static AnalysisFindingDto Hygiene(string id) => new AnalysisFindingDto
        {
            Id = id,
            RuleId = id.Split(':')[0],
            Disposition = FindingDispositions.Hygiene,
        };

        private static AnalysisDocumentDto Document(params AnalysisFindingDto[] findings) =>
            new AnalysisDocumentDto
            {
                Model = new AnalysisIdentityDto { Name = "m", Fingerprint = "sha256:aa" },
                Analyzer = new AnalysisIdentityDto { Name = "a", Version = "1.0", Fingerprint = "sha256:bb" },
                Findings = findings,
            };
    }
}
