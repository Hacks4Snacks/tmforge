namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for <see cref="AnalysisRuleSources"/>: the seam that composes the built-in rules with
    /// any opt-in declarative rules.
    /// </summary>
    [TestClass]
    public class AnalysisRuleSourcesTests
    {
        private const string ValidCustomSpec =
            "{\"rules\":[{\"id\":\"ACME900\",\"severity\":\"error\",\"appliesTo\":\"datastore\"," +
            "\"message\":\"x\",\"assert\":{\"property\":\"Encrypted\",\"present\":true}}]}";

        /// <summary>
        /// With no options, the seam loads the built-in first-party rules.
        /// </summary>
        [TestMethod]
        public void CreateWithoutOptionsLoadsBuiltInRules()
        {
            using (RuleSet set = AnalysisRuleSources.Create())
            {
                Assert.IsTrue(set.Rules.Count > 0);
                Assert.IsTrue(set.Rules.Any(rule => string.Equals(rule.ID, "TM1016", StringComparison.Ordinal)));
            }
        }

        /// <summary>
        /// A custom spec adds its rule alongside the built-in rules.
        /// </summary>
        [TestMethod]
        public void CreateMergesCustomRules()
        {
            string path = WriteSpec(ValidCustomSpec);
            try
            {
                int builtInCount;
                using (RuleSet baseline = AnalysisRuleSources.Create())
                {
                    builtInCount = baseline.Rules.Count;
                }

                using (RuleSet set = AnalysisRuleSources.Create(new RuleSourceOptions(new[] { path })))
                {
                    Assert.AreEqual(builtInCount + 1, set.Rules.Count);
                    Assert.IsTrue(set.Rules.Any(rule => string.Equals(rule.ID, "ACME900", StringComparison.Ordinal)));
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// A custom rule whose id collides with a built-in rule is dropped and reported, so the
        /// built-in namespace always wins.
        /// </summary>
        [TestMethod]
        public void CreateDropsCustomRuleWithDuplicateId()
        {
            string path = WriteSpec(
                "{\"rules\":[{\"id\":\"TM1016\",\"severity\":\"error\",\"appliesTo\":\"datastore\"," +
                "\"message\":\"x\",\"assert\":{\"property\":\"Encrypted\",\"present\":true}}]}");
            try
            {
                int builtInCount;
                using (RuleSet baseline = AnalysisRuleSources.Create())
                {
                    builtInCount = baseline.Rules.Count;
                }

                List<string> diagnostics = new List<string>();
                using (RuleSet set = AnalysisRuleSources.Create(new RuleSourceOptions(new[] { path }, diagnostics.Add)))
                {
                    Assert.AreEqual(builtInCount, set.Rules.Count);
                    Assert.IsTrue(diagnostics.Any(message => message.Contains("TM1016")));
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Legacy v1 files retain their established file-order behavior when two custom rules share
        /// an id: the first declaration loads and the later declaration is dropped by the composition
        /// seam. Version 2 packs use namespaced, order-independent duplicate validation instead.
        /// </summary>
        [TestMethod]
        public void CreatePreservesLegacyDuplicatePrecedence()
        {
            string first = WriteSpec(
                "{\"rules\":[{\"id\":\"ACME-DUP\",\"severity\":\"error\",\"appliesTo\":\"datastore\"," +
                "\"message\":\"first\",\"assert\":{\"property\":\"Encrypted\",\"present\":true}}]}");
            string second = WriteSpec(
                "{\"rules\":[{\"id\":\"ACME-DUP\",\"severity\":\"warning\",\"appliesTo\":\"datastore\"," +
                "\"message\":\"second\",\"assert\":{\"property\":\"Encrypted\",\"present\":true}}]}");
            try
            {
                List<string> diagnostics = new List<string>();
                using (RuleSet set = AnalysisRuleSources.Create(new RuleSourceOptions(new[] { first, second }, diagnostics.Add)))
                {
                    Rule loaded = set.Rules.Single(rule => string.Equals(rule.ID, "ACME-DUP", StringComparison.Ordinal));
                    Assert.AreEqual(MessageSeverity.Error, loaded.Severity);
                    Assert.IsTrue(diagnostics.Any(message => message.Contains("ACME-DUP")));
                }
            }
            finally
            {
                File.Delete(first);
                File.Delete(second);
            }
        }

        private static string WriteSpec(string json)
        {
            string path = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".tmrules.json");
            File.WriteAllText(path, json);
            return path;
        }
    }
}
