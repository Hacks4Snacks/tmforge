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
    }
}
