namespace ThreatModelForge.Api.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Engine;

    /// <summary>
    /// Unit tests for <see cref="EngineService.Analyze"/>, focused on the per-model analysis
    /// selection that travels with the model.
    /// </summary>
    [TestClass]
    public class EngineAnalyzeTest
    {
        /// <summary>
        /// A lone process with no trust boundary produces both a stride-completeness and a
        /// core-hygiene finding, giving a baseline to switch off.
        /// </summary>
        [TestMethod]
        public void Analyze_ProducesBaselineFindings()
        {
            IReadOnlyList<FindingDto> findings = EngineService.Analyze(BuildModel(null));

            Assert.IsTrue(HasRule(findings, "TM1003"), "Expected the missing-trust-boundary finding (stride-completeness).");
            Assert.IsTrue(HasRule(findings, "TM1000"), "Expected the unconnected-component finding (core-hygiene).");
        }

        /// <summary>
        /// Disabling a pack skips only that pack's rules; other packs still run.
        /// </summary>
        [TestMethod]
        public void Analyze_HonorsDisabledPack()
        {
            TmForgeAnalysisDto analysis = new TmForgeAnalysisDto
            {
                DisabledPacks = new[] { "stride-completeness" },
            };

            IReadOnlyList<FindingDto> findings = EngineService.Analyze(BuildModel(analysis));

            Assert.IsFalse(HasRule(findings, "TM1003"), "TM1003 (stride-completeness) should be skipped.");
            Assert.IsTrue(HasRule(findings, "TM1000"), "TM1000 (core-hygiene) should still run.");
        }

        /// <summary>
        /// Disabling by rule id skips only the named rule; other rules still run.
        /// </summary>
        [TestMethod]
        public void Analyze_HonorsDisabledRuleId()
        {
            TmForgeAnalysisDto analysis = new TmForgeAnalysisDto
            {
                DisabledRuleIds = new[] { "TM1000" },
            };

            IReadOnlyList<FindingDto> findings = EngineService.Analyze(BuildModel(analysis));

            Assert.IsFalse(HasRule(findings, "TM1000"), "TM1000 should be skipped by id.");
            Assert.IsTrue(HasRule(findings, "TM1003"), "TM1003 should still run.");
        }

        /// <summary>
        /// A multi-page model is analyzed across every page: findings reference elements on each page,
        /// not only the first, confirming the engine reads the <c>diagrams</c> container.
        /// </summary>
        [TestMethod]
        public void Analyze_AnalyzesAllPages()
        {
            IReadOnlyList<FindingDto> findings = EngineService.Analyze(BuildMultiPageModel());

            Assert.IsTrue(HasRule(findings, "TM1000"), "Expected unconnected-component findings.");
            HashSet<string> flagged = findings.SelectMany(finding => finding.ElementIds).ToHashSet(StringComparer.Ordinal);
            Assert.IsTrue(flagged.Contains("a1"), "Expected a finding on page one's element.");
            Assert.IsTrue(flagged.Contains("b1"), "Expected a finding on page two's element.");
        }

        /// <summary>
        /// Duplicate display names do not fan each finding out to every same-named element; the
        /// stable target identity carried by the rule message resolves to exactly one caller id.
        /// </summary>
        [TestMethod]
        public void Analyze_DuplicateNamesResolveToExactElementIds()
        {
            string[] ids = Enumerable.Range(0, 64).Select(index => "worker-" + index).ToArray();
            TmForgeModelDto model = new TmForgeModelDto
            {
                Elements = ids.Select((id, index) => new TmForgeElementDto
                {
                    Id = id,
                    Kind = "process",
                    Name = "Worker",
                    X = index * 10,
                    Y = 100,
                }).ToArray(),
            };

            List<FindingDto> findings = EngineService.Analyze(model)
                .Where(finding => string.Equals(finding.RuleId, "TM1000", StringComparison.Ordinal))
                .ToList();

            Assert.AreEqual(ids.Length, findings.Count);
            Assert.IsTrue(findings.All(finding => finding.ElementIds.Count == 1));
            CollectionAssert.AreEquivalent(ids, findings.Select(finding => finding.ElementIds[0]).ToArray());
        }

        private static bool HasRule(IReadOnlyList<FindingDto> findings, string ruleId)
        {
            return findings.Any(finding => string.Equals(finding.RuleId, ruleId, StringComparison.Ordinal));
        }

        private static TmForgeModelDto BuildModel(TmForgeAnalysisDto? analysis)
        {
            return new TmForgeModelDto
            {
                Schema = "tmforge-json",
                Version = "0.1",
                Elements = new[]
                {
                    new TmForgeElementDto { Id = "p1", Kind = "process", Name = "Web App", X = 100, Y = 100 },
                },
                Analysis = analysis,
            };
        }

        private static TmForgeModelDto BuildMultiPageModel()
        {
            TmForgeElementDto alpha = new TmForgeElementDto { Id = "a1", Kind = "process", Name = "Alpha", X = 100, Y = 100 };
            TmForgeElementDto beta = new TmForgeElementDto { Id = "b1", Kind = "process", Name = "Beta", X = 100, Y = 100 };
            return new TmForgeModelDto
            {
                Schema = "tmforge-json",
                Version = "0.1",

                // The top-level arrays mirror page one for older readers.
                Elements = new[] { alpha },
                Diagrams = new[]
                {
                    new TmForgeDiagramDto { Id = "d1", Name = "Context", Elements = new[] { alpha } },
                    new TmForgeDiagramDto { Id = "d2", Name = "Payments", Elements = new[] { beta } },
                },
            };
        }
    }
}
