namespace ThreatModelForge.Analysis.Tests
{
    using System;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Analysis.Xml;

    /// <summary>
    /// Unit tests for the <see cref="RuleSetXml"/> class.
    /// </summary>
    [TestClass]
    [DeploymentItem("Test.ruleset")]
    public class RuleSetXmlTests
    {
        /// <summary>
        /// Gets or sets the test context.
        /// </summary>
        public TestContext? TestContext { get; set; }

        /// <summary>
        /// Unit test for the <see cref="RuleSetXml.Load(string)"/> method.
        /// </summary>
        [TestMethod]
        public void LoadTest()
        {
            Assert.IsNotNull(this.TestContext!.DeploymentDirectory);
            string path = System.IO.Path.Combine(
                this.TestContext!.DeploymentDirectory,
                "Test.ruleset");
            RuleSetXml target = RuleSetXml.Load(path);
            Assert.IsNotNull(target);
            Assert.AreEqual("Rules for Hello World project", target.Name);
            Assert.IsFalse(string.IsNullOrWhiteSpace(target.Description));
            Assert.AreEqual("10.0", target.ToolsVersion);
            Assert.AreEqual(2, target.Rules.Count);

            RulesXml? actual1 = target.Rules.FirstOrDefault(
                e => string.Equals(e.AnalyzerId, "Microsoft.Analyzers.ManagedCodeAnalysis"));
            Assert.IsNotNull(actual1);

            Assert.IsFalse(string.IsNullOrWhiteSpace(actual1!.RuleNamespace));
            Assert.AreEqual(4, actual1.Rules.Count);
            foreach (RuleXml rule in actual1.Rules)
            {
                Assert.IsTrue(rule.Id!.StartsWith("CA", StringComparison.Ordinal));
                Assert.AreEqual(RuleAction.Warning, rule.Action);
            }

            RulesXml? actual2 = target.Rules.FirstOrDefault(
                e => string.Equals(e.AnalyzerId, "Microsoft.CodeQuality.Analyzers"));
            Assert.IsNotNull(actual2);

            Assert.IsFalse(string.IsNullOrWhiteSpace(actual2!.RuleNamespace));
            Assert.AreEqual(4, actual2.Rules.Count);
            Assert.IsTrue(actual2.Rules.Any(e => e.Action == RuleAction.Error));
            Assert.IsTrue(actual2.Rules.Any(e => e.Action == RuleAction.None));
            Assert.IsTrue(actual2.Rules.Any(e => e.Action == RuleAction.Info));
        }
    }
}
