namespace ThreatModelForge.Api.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Engine;

    /// <summary>
    /// Tests for the versioned <c>tmforge-analysis</c> document.
    /// </summary>
    /// <remarks>
    /// The document is evidence meant to be stored and compared between runs, so the properties worth
    /// pinning are the ones that make comparison meaningful: it is byte-stable for identical inputs,
    /// every finding carries exactly one disposition, threat-bearing findings can be joined to the
    /// register, and the fingerprints actually move when the thing they describe moves.
    /// </remarks>
    [TestClass]
    public class EngineAnalysisDocumentTest
    {
        private const string PackJson =
            "{\"schema\":\"tmforge-rules\",\"version\":2,\"dialect\":\"urn:tmforge:rules:flat-v1\"," +
            "\"pack\":{\"id\":\"corporate\",\"name\":\"Corporate baseline\",\"version\":\"2.1\"}," +
            "\"categories\":[{\"id\":\"privacy\",\"name\":\"Privacy\"}]," +
            "\"elementTypes\":[{\"id\":\"GE.DS\",\"name\":\"Data store\",\"parentId\":\"ROOT\"}]," +
            "\"properties\":[{\"name\":\"Encrypted\",\"allowedValues\":[\"No\",\"At-rest\"],\"elementTypeIds\":[\"GE.DS\"]}]," +
            "\"rules\":[{\"id\":\"CORP-1\",\"severity\":\"error\",\"categoryId\":\"privacy\"," +
            "\"appliesTo\":\"datastore\",\"message\":\"{name} must encrypt data at rest.\"," +
            "\"helpText\":\"Set Encrypted to At-rest.\"," +
            "\"assert\":{\"property\":\"Encrypted\",\"equals\":\"At-rest\"}}]}";

        /// <summary>The document declares what it is, so a reader can refuse what it does not know.</summary>
        [TestMethod]
        public void DocumentDeclaresItsSchemaAndFingerprints()
        {
            AnalysisDocumentDto document = EngineService.DescribeAnalysis(SampleModel());

            Assert.AreEqual("tmforge-analysis", document.Schema);
            Assert.AreEqual(1, document.Version);
            Assert.IsTrue(document.Model.Fingerprint.StartsWith("sha256:", StringComparison.Ordinal));
            Assert.IsTrue(document.Analyzer.Fingerprint.StartsWith("sha256:", StringComparison.Ordinal));
            Assert.IsFalse(string.IsNullOrEmpty(document.Analyzer.Name));
            Assert.IsFalse(string.IsNullOrEmpty(document.Analyzer.Version));
        }

        /// <summary>
        /// Two runs over the same inputs produce byte-identical documents. This is the property the
        /// artifact exists for: a consumer diffs two of these to see what changed, which only works if
        /// nothing else churns. It is also why the document carries no timestamp.
        /// </summary>
        [TestMethod]
        public void DocumentIsByteStableAcrossRuns()
        {
            string first = JsonSerializer.Serialize(EngineService.DescribeAnalysis(SampleModel()));
            string second = JsonSerializer.Serialize(EngineService.DescribeAnalysis(SampleModel()));

            Assert.AreEqual(first, second);
        }

        /// <summary>Every finding has exactly one disposition, drawn from the defined vocabulary.</summary>
        [TestMethod]
        public void EveryFindingCarriesOneKnownDisposition()
        {
            AnalysisDocumentDto document = EngineService.DescribeAnalysis(SampleModel(), Rules());

            Assert.IsTrue(document.Findings.Count > 0);
            foreach (AnalysisFindingDto finding in document.Findings)
            {
                Assert.IsTrue(
                    FindingDispositions.IsDefined(finding.Disposition),
                    $"'{finding.Id}' has disposition '{finding.Disposition}'.");
            }
        }

        /// <summary>
        /// Threat-bearing findings link to the register; hygiene findings stay findings. This is the
        /// separation the work item exists for: a reviewer should not have to accept "this diagram has
        /// no trust boundary" as a risk in order to clear a gate.
        /// </summary>
        [TestMethod]
        public void ThreatBearingFindingsLinkToTheRegisterAndHygieneDoesNot()
        {
            TmForgeModelDto model = SampleModel();
            AnalysisDocumentDto document = EngineService.DescribeAnalysis(model, Rules());
            IReadOnlyList<ThreatDto> threats = EngineService.GenerateThreats(model, Rules());

            AnalysisFindingDto threatBearing = document.Findings
                .First(finding => FindingDispositions.IsThreatBearing(finding.Disposition));
            Assert.IsNotNull(threatBearing.ThreatId);
            Assert.IsTrue(
                threats.Any(threat => threat.Id == threatBearing.ThreatId),
                $"'{threatBearing.ThreatId}' is not in the generated register.");

            AnalysisFindingDto hygiene = document.Findings
                .First(finding => finding.Disposition == FindingDispositions.Hygiene);
            Assert.IsNull(hygiene.ThreatId);

            // A structural rule such as "no trust boundary" must never be threat-bearing.
            AnalysisFindingDto structural = document.Findings.First(finding => finding.RuleId == "TM1003");
            Assert.AreEqual(FindingDispositions.Hygiene, structural.Disposition);
        }

        /// <summary>The author's triage decides the disposition of a threat-bearing finding.</summary>
        /// <param name="state">The recorded lifecycle state.</param>
        /// <param name="expected">The disposition it should produce.</param>
        [TestMethod]
        [DataRow("Accepted", FindingDispositions.Accepted)]
        [DataRow("Mitigated", FindingDispositions.Mitigated)]
        [DataRow("NeedsInvestigation", FindingDispositions.Unresolved)]
        [DataRow("Open", FindingDispositions.GeneratedThreat)]
        public void TriageDecidesTheDisposition(string state, string expected)
        {
            TmForgeModelDto model = SampleModel();
            string threatId = EngineService.DescribeAnalysis(model, Rules()).Findings
                .First(finding => FindingDispositions.IsThreatBearing(finding.Disposition))
                .ThreatId!;

            TmForgeModelDto triaged = WithTriage(model, threatId, state);
            AnalysisFindingDto finding = EngineService.DescribeAnalysis(triaged, Rules()).Findings
                .First(entry => entry.ThreatId == threatId);

            Assert.AreEqual(expected, finding.Disposition);
        }

        /// <summary>
        /// Accepting a risk changes the disposition without claiming the model changed. Folding triage
        /// into the model fingerprint would report a model edit every time somebody triaged a threat.
        /// </summary>
        [TestMethod]
        public void TriageDoesNotChangeTheModelFingerprint()
        {
            TmForgeModelDto model = SampleModel();
            AnalysisDocumentDto before = EngineService.DescribeAnalysis(model, Rules());
            string threatId = before.Findings
                .First(finding => FindingDispositions.IsThreatBearing(finding.Disposition))
                .ThreatId!;

            AnalysisDocumentDto after = EngineService.DescribeAnalysis(WithTriage(model, threatId, "Accepted"), Rules());

            Assert.AreEqual(before.Model.Fingerprint, after.Model.Fingerprint);
            Assert.AreEqual(before.Analyzer.Fingerprint, after.Analyzer.Fingerprint);
            Assert.AreEqual(
                FindingDispositions.Accepted,
                after.Findings.First(finding => finding.ThreatId == threatId).Disposition);
        }

        /// <summary>Editing the model moves the model fingerprint, which is how staleness is detected.</summary>
        [TestMethod]
        public void EditingTheModelChangesTheModelFingerprint()
        {
            AnalysisDocumentDto before = EngineService.DescribeAnalysis(SampleModel());

            TmForgeModelDto edited = SampleModel();
            edited = new TmForgeModelDto
            {
                Elements = edited.Elements!.Concat(new[]
                {
                    new TmForgeElementDto { Id = "p2", Kind = "process", Name = "Reporting" },
                }).ToList(),
                Flows = edited.Flows,
            };

            Assert.AreNotEqual(before.Model.Fingerprint, EngineService.DescribeAnalysis(edited).Model.Fingerprint);
        }

        /// <summary>
        /// Loading a rule pack or disabling a rule moves the analyzer fingerprint, because either
        /// changes which rules ran.
        /// </summary>
        [TestMethod]
        public void RuleSelectionChangesTheAnalyzerFingerprint()
        {
            TmForgeModelDto model = SampleModel();
            string plain = EngineService.DescribeAnalysis(model).Analyzer.Fingerprint;
            string withPack = EngineService.DescribeAnalysis(model, Rules()).Analyzer.Fingerprint;

            TmForgeModelDto restricted = new TmForgeModelDto
            {
                Elements = model.Elements,
                Flows = model.Flows,
                Analysis = new TmForgeAnalysisDto { DisabledRuleIds = new[] { "TM1003" } },
            };

            Assert.AreNotEqual(plain, withPack);
            Assert.AreNotEqual(plain, EngineService.DescribeAnalysis(restricted).Analyzer.Fingerprint);
        }

        /// <summary>
        /// The same selection listed in a different order is the same selection, so it must fingerprint
        /// identically rather than looking like a different rule set.
        /// </summary>
        [TestMethod]
        public void DisabledSelectionOrderDoesNotChangeTheAnalyzerFingerprint()
        {
            string forward = EngineService.DescribeAnalysis(WithDisabled("TM1003", "TM1005")).Analyzer.Fingerprint;
            string reversed = EngineService.DescribeAnalysis(WithDisabled("TM1005", "TM1003")).Analyzer.Fingerprint;

            Assert.AreEqual(forward, reversed);
        }

        /// <summary>A document the engine produced is coherent by its own validator.</summary>
        [TestMethod]
        public void ProducedDocumentValidates()
        {
            IReadOnlyList<string> problems =
                AnalysisDocumentValidator.Validate(EngineService.DescribeAnalysis(SampleModel(), Rules()));

            Assert.AreEqual(0, string.Join("; ", problems).Length, string.Join("; ", problems));
        }

        /// <summary>A custom rule's finding names its pack, and the document accounts for that pack.</summary>
        [TestMethod]
        public void CustomRuleEvidenceNamesItsPack()
        {
            AnalysisDocumentDto document = EngineService.DescribeAnalysis(SampleModel(), Rules());

            AnalysisFindingDto finding = document.Findings.First(entry => entry.RuleId == "corporate/CORP-1");
            Assert.AreEqual("s1", finding.ElementIds.Single());
            Assert.IsTrue(FindingDispositions.IsThreatBearing(finding.Disposition));
            Assert.IsTrue(document.RulePacks.Any(pack => pack.Id == "corporate"));
        }

        private static EngineRuleOptions Rules()
        {
            return new EngineRuleOptions
            {
                Sources = new[] { new RuleSourceDto { Name = "corporate.tmrules.json", Json = PackJson } },
            };
        }

        private static TmForgeModelDto WithDisabled(params string[] ruleIds)
        {
            TmForgeModelDto model = SampleModel();
            return new TmForgeModelDto
            {
                Elements = model.Elements,
                Flows = model.Flows,
                Analysis = new TmForgeAnalysisDto { DisabledRuleIds = ruleIds },
            };
        }

        private static TmForgeModelDto WithTriage(TmForgeModelDto model, string threatId, string state)
        {
            return new TmForgeModelDto
            {
                Elements = model.Elements,
                Flows = model.Flows,
                Threats = new[]
                {
                    new ThreatStateDto { Id = threatId, State = state, Justification = "reviewed" },
                },
            };
        }

        private static TmForgeModelDto SampleModel()
        {
            return new TmForgeModelDto
            {
                Elements = new[]
                {
                    new TmForgeElementDto
                    {
                        Id = "s1",
                        Kind = "datastore",
                        Name = "Ledger",
                        Properties = new Dictionary<string, string> { ["Encrypted"] = "No" },
                    },
                    new TmForgeElementDto { Id = "p1", Kind = "process", Name = "Checkout" },
                    new TmForgeElementDto { Id = "e1", Kind = "external", Name = "Customer" },
                },
                Flows = new[]
                {
                    new TmForgeFlowDto { Id = "f1", Source = "e1", Target = "p1", Name = "order" },
                    new TmForgeFlowDto { Id = "f2", Source = "p1", Target = "s1", Name = "write" },
                },
            };
        }
    }
}
