namespace ThreatModelForge.Api.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Engine;

    /// <summary>
    /// Tests for <see cref="AnalysisDocumentDiff"/>, the findings delta between two stored analyses.
    /// </summary>
    [TestClass]
    public class EngineAnalysisDiffTest
    {
        /// <summary>
        /// The property the whole comparison exists for: findings are matched on identity, so one that
        /// went away and one that arrived are named rather than netted out into a count that happens to
        /// be unchanged.
        /// </summary>
        [TestMethod]
        public void FindingsAreMatchedByIdentityRatherThanCounted()
        {
            AnalysisDocumentDto before = Document(Finding("TM1021:d:a:0"), Finding("TM1029:d:b:0"));
            AnalysisDocumentDto after = Document(Finding("TM1029:d:b:0"), Finding("TM1014:d:c:0"));

            AnalysisDifference difference = AnalysisDocumentDiff.Compare(before, after);

            Assert.AreEqual(before.Findings.Count, after.Findings.Count, "the counts are equal on purpose");
            Assert.AreEqual("TM1014:d:c:0", difference.Introduced.Single().Id);
            Assert.AreEqual("TM1021:d:a:0", difference.Resolved.Single().Id);
            Assert.AreEqual(1, difference.Unchanged);
        }

        /// <summary>
        /// A finding's message carries the element's display name, so renaming an element rewrites it.
        /// That must not read as a finding change, or every cosmetic edit would look like security
        /// churn and the delta would stop being worth reading.
        /// </summary>
        [TestMethod]
        public void RenamingTheElementAFindingIsAboutIsNotAChange()
        {
            AnalysisDocumentDto before = Document(Finding("TM1029:d:b:0", message: "The Web App ... is not audited"));
            AnalysisDocumentDto after = Document(Finding("TM1029:d:b:0", message: "The Storefront ... is not audited"));

            AnalysisDifference difference = AnalysisDocumentDiff.Compare(before, after);

            Assert.IsTrue(difference.IsEmpty);
            Assert.AreEqual(1, difference.Unchanged);
        }

        /// <summary>
        /// Suppressing a finding records it with a new disposition rather than dropping it, so the
        /// delta must report it as reclassified. Reporting it as resolved would say the underlying
        /// condition went away, which is exactly what a suppression does not claim.
        /// </summary>
        [TestMethod]
        public void ASuppressedFindingIsReclassifiedRatherThanResolved()
        {
            AnalysisDocumentDto before = Document(Finding("TM1029:d:b:0", disposition: FindingDispositions.GeneratedThreat));
            AnalysisDocumentDto after = Document(Finding("TM1029:d:b:0", disposition: FindingDispositions.Suppressed));

            AnalysisDifference difference = AnalysisDocumentDiff.Compare(before, after);

            Assert.AreEqual(0, difference.Resolved.Count);
            Assert.AreEqual(0, difference.Introduced.Count);
            AnalysisFindingChange change = difference.Reclassified.Single();
            Assert.AreEqual(FindingDispositions.GeneratedThreat, change.Before.Disposition);
            Assert.AreEqual(FindingDispositions.Suppressed, change.After.Disposition);
        }

        /// <summary>A rule whose severity was reconfigured is reported as reclassified.</summary>
        [TestMethod]
        public void AChangedSeverityIsReclassified()
        {
            AnalysisDocumentDto before = Document(Finding("TM1029:d:b:0", severity: "warning"));
            AnalysisDocumentDto after = Document(Finding("TM1029:d:b:0", severity: "error"));

            AnalysisFindingChange change = AnalysisDocumentDiff.Compare(before, after).Reclassified.Single();

            Assert.AreEqual("warning", change.Before.Severity);
            Assert.AreEqual("error", change.After.Severity);
        }

        /// <summary>Two analyses that reached the same conclusions report nothing.</summary>
        [TestMethod]
        public void IdenticalAnalysesReportNoDifference()
        {
            AnalysisDocumentDto document = Document(Finding("TM1021:d:a:0"), Finding("TM1029:d:b:0"));

            AnalysisDifference difference = AnalysisDocumentDiff.Compare(document, document);

            Assert.IsTrue(difference.IsEmpty);
            Assert.AreEqual(2, difference.Unchanged);
            Assert.AreEqual(0, difference.Warnings.Count);
        }

        /// <summary>
        /// A changed rule selection is reported, because otherwise a finding that arrived only because
        /// a new rule was switched on would read as a finding the change introduced.
        /// </summary>
        [TestMethod]
        public void AChangedRuleSelectionIsWarnedAboutRatherThanFoldedIn()
        {
            AnalysisDocumentDto before = Document(Finding("TM1021:d:a:0"));
            AnalysisDocumentDto after = Document(Finding("TM1021:d:a:0"), Finding("CORP-1:d:a:0"));
            after = new AnalysisDocumentDto
            {
                Model = after.Model,
                Analyzer = new AnalysisIdentityDto { Name = "tmforge", Version = "1", Fingerprint = "sha256:other" },
                Findings = after.Findings,
            };

            AnalysisDifference difference = AnalysisDocumentDiff.Compare(before, after);

            Assert.AreEqual("CORP-1:d:a:0", difference.Introduced.Single().Id);
            Assert.IsTrue(
                difference.Warnings.Any(warning => warning.Contains("rule selection")),
                string.Join(" | ", difference.Warnings));
        }

        /// <summary>Comparing analyses of two different models is reported as the mistake it usually is.</summary>
        [TestMethod]
        public void ComparingTwoDifferentModelsIsWarnedAbout()
        {
            AnalysisDocumentDto before = Document(Finding("TM1021:d:a:0"));
            AnalysisDocumentDto after = new AnalysisDocumentDto
            {
                Model = new AnalysisIdentityDto { Name = "payments", Version = "1", Fingerprint = "sha256:model-b" },
                Analyzer = before.Analyzer,
                Findings = before.Findings,
            };

            AnalysisDifference difference = AnalysisDocumentDiff.Compare(before, after);

            Assert.IsTrue(
                difference.Warnings.Any(warning => warning.Contains("different models")),
                string.Join(" | ", difference.Warnings));
        }

        /// <summary>
        /// The fingerprints cover every input to an analysis except the suppression list, and the
        /// documents carry no timestamp, so identical fingerprints with differing findings has exactly
        /// one ordinary explanation and the tool should name it rather than leave a reader guessing.
        /// </summary>
        [TestMethod]
        public void IdenticalFingerprintsWithDifferingFindingsNamesTheRemainingInput()
        {
            AnalysisDocumentDto before = Document(Finding("TM1029:d:b:0", disposition: FindingDispositions.GeneratedThreat));
            AnalysisDocumentDto after = Document(Finding("TM1029:d:b:0", disposition: FindingDispositions.Suppressed));

            AnalysisDifference difference = AnalysisDocumentDiff.Compare(before, after);

            Assert.IsTrue(
                difference.Warnings.Any(warning => warning.Contains("Suppressions")),
                string.Join(" | ", difference.Warnings));
        }

        /// <summary>The result is ordered by identity so two runs of the same comparison agree.</summary>
        [TestMethod]
        public void IntroducedAndResolvedAreOrderedByIdentity()
        {
            AnalysisDocumentDto before = Document(Finding("TM1029:d:b:0"), Finding("TM1021:d:a:0"));
            AnalysisDocumentDto after = Document(Finding("TM1099:d:z:0"), Finding("TM1014:d:c:0"));

            AnalysisDifference difference = AnalysisDocumentDiff.Compare(before, after);

            CollectionAssert.AreEqual(
                new[] { "TM1014:d:c:0", "TM1099:d:z:0" },
                difference.Introduced.Select(finding => finding.Id).ToArray());
            CollectionAssert.AreEqual(
                new[] { "TM1021:d:a:0", "TM1029:d:b:0" },
                difference.Resolved.Select(finding => finding.Id).ToArray());
        }

        private static AnalysisDocumentDto Document(params AnalysisFindingDto[] findings)
        {
            return new AnalysisDocumentDto
            {
                Model = new AnalysisIdentityDto { Name = "webshop", Version = "1", Fingerprint = "sha256:model-a" },
                Analyzer = new AnalysisIdentityDto { Name = "tmforge", Version = "1", Fingerprint = "sha256:rules-a" },
                Findings = findings,
            };
        }

        private static AnalysisFindingDto Finding(
            string id,
            string severity = "warning",
            string disposition = FindingDispositions.GeneratedThreat,
            string message = "finding")
        {
            return new AnalysisFindingDto
            {
                Id = id,
                RuleId = id.Split(':')[0],
                Severity = severity,
                Disposition = disposition,
                Message = message,
                Diagram = "Diagram 1",
                ElementIds = new List<string>(),
            };
        }
    }
}
