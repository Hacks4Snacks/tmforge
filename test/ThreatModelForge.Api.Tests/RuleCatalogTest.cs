namespace ThreatModelForge.Api.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests guarding the analysis rule catalog surfaced by <see cref="EngineService"/>.
    /// </summary>
    [TestClass]
    public class RuleCatalogTest
    {
        private static readonly HashSet<string> ValidPacks = new HashSet<string>(StringComparer.Ordinal)
        {
            "core-hygiene",
            "stride-completeness",
            "input-validation",
            "data-protection",
            "transport-security",
            "identity-access",
        };

        private static readonly HashSet<string> ValidSeverities = new HashSet<string>(StringComparer.Ordinal)
        {
            "info",
            "warning",
            "error",
        };

        /// <summary>
        /// The engine exposes at least one analysis rule.
        /// </summary>
        [TestMethod]
        public void GetRules_IsNotEmpty()
        {
            Assert.IsTrue(EngineService.GetRules().Count > 0, "The rule catalog must not be empty.");
        }

        /// <summary>
        /// Rule identifiers are unique so selection by id is unambiguous.
        /// </summary>
        [TestMethod]
        public void GetRules_HaveUniqueIds()
        {
            List<string> ids = EngineService.GetRules().Select(rule => rule.Id).ToList();
            CollectionAssert.AllItemsAreUnique(ids, "Rule ids must be unique.");
        }

        /// <summary>
        /// Every rule declares one of the known packs so the validation UI can group it.
        /// </summary>
        [TestMethod]
        public void GetRules_BelongToAKnownPack()
        {
            foreach (RuleDto rule in EngineService.GetRules())
            {
                Assert.IsTrue(
                    ValidPacks.Contains(rule.Pack),
                    $"Rule {rule.Id} has unknown pack '{rule.Pack}'.");
            }
        }

        /// <summary>
        /// Every rule carries a valid severity and a non-empty description for the UI.
        /// </summary>
        [TestMethod]
        public void GetRules_HaveSeverityAndDescription()
        {
            foreach (RuleDto rule in EngineService.GetRules())
            {
                Assert.IsTrue(
                    ValidSeverities.Contains(rule.Severity),
                    $"Rule {rule.Id} has invalid severity '{rule.Severity}'.");
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(rule.Description),
                    $"Rule {rule.Id} has no description.");
            }
        }

        /// <summary>
        /// The property-aware security rules are assigned to their cohesive domain packs.
        /// </summary>
        [TestMethod]
        public void SecurityRules_AreAssignedToTheirDomainPacks()
        {
            IReadOnlyList<RuleDto> rules = EngineService.GetRules();
            string PackOf(string ruleId) => rules
                .Where(rule => string.Equals(rule.Id, ruleId, StringComparison.Ordinal))
                .Select(rule => rule.Pack)
                .FirstOrDefault() ?? string.Empty;

            Assert.AreEqual("data-protection", PackOf("TM1014"), "TM1014 (unencrypted secret store) belongs to data-protection.");
            Assert.AreEqual("identity-access", PackOf("TM1015"), "TM1015 (unauthenticated boundary process) belongs to identity-access.");
            Assert.AreEqual("transport-security", PackOf("TM1016"), "TM1016 (cleartext trust-boundary crossing) belongs to transport-security.");
        }

        /// <summary>
        /// Each pack's advertised count matches the rules assigned to it, and every rule is covered.
        /// </summary>
        [TestMethod]
        public void GetRulePacks_CountsMatchRules()
        {
            IReadOnlyList<RuleDto> rules = EngineService.GetRules();
            IReadOnlyList<RulePackDto> packs = EngineService.GetRulePacks();

            foreach (RulePackDto pack in packs)
            {
                int expected = rules.Count(rule => string.Equals(rule.Pack, pack.Id, StringComparison.Ordinal));
                Assert.AreEqual(expected, pack.Count, $"Pack {pack.Id} count mismatch.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(pack.Name), $"Pack {pack.Id} has no display name.");
            }

            Assert.AreEqual(rules.Count, packs.Sum(pack => pack.Count), "Rule packs must cover every rule exactly once.");
        }

        /// <summary>
        /// The known packs are returned in a stable presentation order.
        /// </summary>
        [TestMethod]
        public void GetRulePacks_AreInPresentationOrder()
        {
            List<string> ids = EngineService.GetRulePacks().Select(pack => pack.Id).ToList();
            CollectionAssert.AreEqual(
                new[] { "core-hygiene", "stride-completeness", "input-validation", "data-protection", "transport-security", "identity-access" },
                ids,
                "Rule packs must be returned in presentation order.");
        }
    }
}
