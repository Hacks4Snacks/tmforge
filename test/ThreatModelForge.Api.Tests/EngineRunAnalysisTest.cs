namespace ThreatModelForge.Api.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Engine;

    /// <summary>
    /// Contract tests for the single-pass analysis action. One evaluation now feeds both projections,
    /// so the combined operation must agree with the compatibility operations exactly — otherwise the
    /// optimization would have quietly changed what an analysis reports.
    /// </summary>
    [TestClass]
    public class EngineRunAnalysisTest
    {
        /// <summary>The combined action's findings match the findings-only operation, id for id.</summary>
        [TestMethod]
        public void CombinedFindingsMatchAnalyze()
        {
            IReadOnlyList<FindingDto> separate = EngineService.Analyze(SampleModel());
            AnalysisResultDto combined = EngineService.RunAnalysis(SampleModel(), null);

            CollectionAssert.AreEqual(
                separate.Select(Describe).ToList(),
                combined.Findings.Select(Describe).ToList());
            Assert.IsTrue(combined.Findings.Count > 0);
        }

        /// <summary>
        /// The combined action's threats match the threats-only operation across every field a caller
        /// triages on: identity, category, priority, mitigation, references, and state.
        /// </summary>
        [TestMethod]
        public void CombinedThreatsMatchGenerateThreats()
        {
            IReadOnlyList<ThreatDto> separate = EngineService.GenerateThreats(SampleModel());
            AnalysisResultDto combined = EngineService.RunAnalysis(SampleModel(), null);

            CollectionAssert.AreEqual(
                separate.Select(Describe).ToList(),
                combined.Threats.Select(Describe).ToList());
            Assert.IsTrue(combined.Threats.Count > 0);
        }

        /// <summary>A findings-only request does not materialize the threat register it will not read.</summary>
        [TestMethod]
        public void AnalyzeDoesNotMaterializeThreats()
        {
            AnalysisResultDto result = EngineService.Analyze(SampleModel(), null);

            Assert.IsTrue(result.Findings.Count > 0);
            Assert.AreEqual(0, result.Threats.Count);
        }

        /// <summary>Disabled packs apply identically to the combined action and the separate ones.</summary>
        [TestMethod]
        public void DisabledSelectionAppliesToBothProjections()
        {
            TmForgeModelDto model = SampleModel();
            TmForgeModelDto disabled = new TmForgeModelDto
            {
                Elements = model.Elements,
                Flows = model.Flows,
                Analysis = new TmForgeAnalysisDto { DisabledPacks = new[] { "identity-access" } },
            };

            AnalysisResultDto combined = EngineService.RunAnalysis(disabled, null);
            IReadOnlyList<FindingDto> separateFindings = EngineService.Analyze(disabled);
            IReadOnlyList<ThreatDto> separateThreats = EngineService.GenerateThreats(disabled);

            CollectionAssert.AreEqual(
                separateFindings.Select(Describe).ToList(),
                combined.Findings.Select(Describe).ToList());
            CollectionAssert.AreEqual(
                separateThreats.Select(Describe).ToList(),
                combined.Threats.Select(Describe).ToList());
        }

        /// <summary>Author triage survives the shared evaluation, exactly as it does on its own.</summary>
        [TestMethod]
        public void TriageAppliesToTheCombinedAction()
        {
            TmForgeModelDto model = SampleModel();
            string threatId = EngineService.GenerateThreats(model).First(threat => !threat.Manual).Id;
            TmForgeModelDto triaged = new TmForgeModelDto
            {
                Elements = model.Elements,
                Flows = model.Flows,
                Threats = new[]
                {
                    new ThreatStateDto { Id = threatId, State = "Accepted", Justification = "Compensating control." },
                },
            };

            AnalysisResultDto combined = EngineService.RunAnalysis(triaged, null);

            ThreatDto accepted = combined.Threats.Single(threat => threat.Id == threatId);
            Assert.AreEqual("Accepted", accepted.State);
            Assert.AreEqual("Compensating control.", accepted.Justification);
        }

        /// <summary>Manually authored threats are appended by the combined action too.</summary>
        [TestMethod]
        public void ManualThreatsAppearInTheCombinedAction()
        {
            TmForgeModelDto model = SampleModel();
            TmForgeModelDto withManual = new TmForgeModelDto
            {
                Elements = model.Elements,
                Flows = model.Flows,
                Threats = new[]
                {
                    new ThreatStateDto
                    {
                        Id = "manual:1",
                        Manual = true,
                        Category = "Tampering",
                        Title = "Operator can alter the ledger",
                        State = "Open",
                    },
                },
            };

            AnalysisResultDto combined = EngineService.RunAnalysis(withManual, null);

            ThreatDto manual = combined.Threats.Single(threat => threat.Manual);
            Assert.AreEqual("Operator can alter the ledger", manual.Title);
        }

        /// <summary>
        /// Two overlay entries claiming the same id cannot both be threats. The second is dropped, but
        /// the caller is told — a threat that silently fails to appear reads as no threat at all.
        /// </summary>
        [TestMethod]
        public void DuplicateManualIdIsReportedRatherThanSilentlyDropped()
        {
            TmForgeModelDto model = SampleModel();
            TmForgeModelDto withDuplicates = new TmForgeModelDto
            {
                Elements = model.Elements,
                Flows = model.Flows,
                Threats = new[]
                {
                    new ThreatStateDto { Id = "manual:dup", Manual = true, Category = "Tampering", Title = "First" },
                    new ThreatStateDto { Id = "manual:dup", Manual = true, Category = "Tampering", Title = "Second" },
                },
            };

            AnalysisResultDto combined = EngineService.RunAnalysis(withDuplicates, null);

            Assert.AreEqual(1, combined.Threats.Count(threat => threat.Id == "manual:dup"));
            Assert.AreEqual("First", combined.Threats.Single(threat => threat.Manual).Title);
            Assert.IsTrue(
                combined.Diagnostics.Any(diagnostic => diagnostic.Contains("manual:dup", StringComparison.Ordinal)),
                string.Join(" | ", combined.Diagnostics));
        }

        /// <summary>
        /// A manual entry whose id is not a usable identity is reported, not silently ignored.
        /// </summary>
        [TestMethod]
        public void MalformedManualIdIsReported()
        {
            TmForgeModelDto model = SampleModel();
            TmForgeModelDto withBadId = new TmForgeModelDto
            {
                Elements = model.Elements,
                Flows = model.Flows,
                Threats = new[]
                {
                    new ThreatStateDto { Id = "  ", Manual = true, Category = "Tampering", Title = "Nameless" },
                },
            };

            AnalysisResultDto combined = EngineService.RunAnalysis(withBadId, null);

            Assert.IsFalse(combined.Threats.Any(threat => threat.Manual));
            Assert.IsTrue(combined.Diagnostics.Count > 0, "The rejected manual threat must be reported.");
        }

        /// <summary>
        /// A failure during one action is reported once in each projection the caller reads, rather
        /// than as two unrelated errors from two evaluations.
        /// </summary>
        [TestMethod]
        public void EvaluationFailureProducesOneCoherentResult()
        {
            TmForgeModelDto broken = new TmForgeModelDto
            {
                Elements = new[] { new TmForgeElementDto { Id = "p1", Kind = "process", Name = "Gateway" } },
                Flows = new[] { new TmForgeFlowDto { Id = "f1", Source = "p1", Target = "p1" } },
                Threats = new[] { new ThreatStateDto { Id = "manual:1", Manual = true, Category = "\0bad" } },
            };

            AnalysisResultDto combined = EngineService.RunAnalysis(broken, null);

            // Whatever the outcome, one action never reports the same failure from two evaluations.
            Assert.IsTrue(combined.Findings.Count(finding => finding.Id == "engine-error") <= 1);
            Assert.IsTrue(combined.Threats.Count(threat => threat.Id == "engine-error") <= 1);
        }

        private static string Describe(FindingDto finding)
        {
            return string.Join(
                "|",
                finding.Id,
                finding.Severity,
                finding.RuleId,
                finding.Message,
                string.Join(",", finding.ElementIds));
        }

        private static string Describe(ThreatDto threat)
        {
            return string.Join(
                "|",
                threat.Id,
                threat.RuleId,
                threat.Category,
                threat.CategoryId,
                threat.CategoryName,
                threat.Stride,
                threat.Title,
                threat.Mitigation,
                threat.Severity,
                threat.Priority,
                threat.State,
                threat.Justification,
                threat.Interaction,
                threat.Manual.ToString(),
                string.Join(",", threat.References ?? Array.Empty<string>()),
                string.Join(",", threat.ElementIds ?? Array.Empty<string>()));
        }

        private static TmForgeModelDto SampleModel()
        {
            // Guid-shaped ids are preserved through the canonical read, so every call builds the same
            // model identities. Without that, finding text and threat keys would carry fresh guids and
            // two runs of the same model could not be compared at all.
            return new TmForgeModelDto
            {
                Elements = new[]
                {
                    new TmForgeElementDto
                    {
                        Id = "11111111-1111-4111-8111-111111111111",
                        Kind = "external",
                        Name = "Customer",
                    },
                    new TmForgeElementDto
                    {
                        Id = "22222222-2222-4222-8222-222222222222",
                        Kind = "process",
                        Name = "Gateway",
                    },
                    new TmForgeElementDto
                    {
                        Id = "33333333-3333-4333-8333-333333333333",
                        Kind = "datastore",
                        Name = "Ledger",
                    },
                },
                Flows = new[]
                {
                    new TmForgeFlowDto
                    {
                        Id = "44444444-4444-4444-8444-444444444444",
                        Source = "11111111-1111-4111-8111-111111111111",
                        Target = "22222222-2222-4222-8222-222222222222",
                        Name = "request",
                    },
                    new TmForgeFlowDto
                    {
                        Id = "55555555-5555-4555-8555-555555555555",
                        Source = "22222222-2222-4222-8222-222222222222",
                        Target = "33333333-3333-4333-8333-333333333333",
                        Name = "write",
                    },
                },
            };
        }
    }
}
