namespace ThreatModelForge.Api.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Engine;

    /// <summary>
    /// Unit tests for <see cref="EngineService.Validate"/>, focused on the per-model validation
    /// selection that travels with the model.
    /// </summary>
    [TestClass]
    public class EngineValidateTest
    {
        /// <summary>
        /// A lone process with no trust boundary produces both a stride-completeness and a
        /// core-hygiene finding, giving a baseline to switch off.
        /// </summary>
        [TestMethod]
        public void Validate_ProducesBaselineFindings()
        {
            IReadOnlyList<FindingDto> findings = EngineService.Validate(BuildModel(null));

            Assert.IsTrue(HasRule(findings, "TM1003"), "Expected the missing-trust-boundary finding (stride-completeness).");
            Assert.IsTrue(HasRule(findings, "TM1000"), "Expected the unconnected-component finding (core-hygiene).");
        }

        /// <summary>
        /// Disabling a pack skips only that pack's rules; other packs still run.
        /// </summary>
        [TestMethod]
        public void Validate_HonorsDisabledPack()
        {
            TmForgeValidationDto validation = new TmForgeValidationDto
            {
                DisabledPacks = new[] { "stride-completeness" },
            };

            IReadOnlyList<FindingDto> findings = EngineService.Validate(BuildModel(validation));

            Assert.IsFalse(HasRule(findings, "TM1003"), "TM1003 (stride-completeness) should be skipped.");
            Assert.IsTrue(HasRule(findings, "TM1000"), "TM1000 (core-hygiene) should still run.");
        }

        /// <summary>
        /// Disabling by rule id skips only the named rule; other rules still run.
        /// </summary>
        [TestMethod]
        public void Validate_HonorsDisabledRuleId()
        {
            TmForgeValidationDto validation = new TmForgeValidationDto
            {
                DisabledRuleIds = new[] { "TM1000" },
            };

            IReadOnlyList<FindingDto> findings = EngineService.Validate(BuildModel(validation));

            Assert.IsFalse(HasRule(findings, "TM1000"), "TM1000 should be skipped by id.");
            Assert.IsTrue(HasRule(findings, "TM1003"), "TM1003 should still run.");
        }

        /// <summary>
        /// A multi-page model is analyzed across every page: findings reference elements on each page,
        /// not only the first, confirming the engine reads the <c>diagrams</c> container.
        /// </summary>
        [TestMethod]
        public void Validate_AnalyzesAllPages()
        {
            IReadOnlyList<FindingDto> findings = EngineService.Validate(BuildMultiPageModel());

            Assert.IsTrue(HasRule(findings, "TM1000"), "Expected unconnected-component findings.");
            HashSet<string> flagged = findings.SelectMany(finding => finding.ElementIds).ToHashSet(StringComparer.Ordinal);
            Assert.IsTrue(flagged.Contains("a1"), "Expected a finding on page one's element.");
            Assert.IsTrue(flagged.Contains("b1"), "Expected a finding on page two's element.");
        }

        private static bool HasRule(IReadOnlyList<FindingDto> findings, string ruleId)
        {
            return findings.Any(finding => string.Equals(finding.RuleId, ruleId, StringComparison.Ordinal));
        }

        private static TmForgeModelDto BuildModel(TmForgeValidationDto? validation)
        {
            return new TmForgeModelDto
            {
                Schema = "tmforge-json",
                Version = "0.1",
                Elements = new[]
                {
                    new TmForgeElementDto { Id = "p1", Kind = "process", Name = "Web App", X = 100, Y = 100 },
                },
                Validation = validation,
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
