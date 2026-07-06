namespace ThreatModelForge.Analysis.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit test for the <see cref="RuleSet"/> class.
    /// </summary>
    [TestClass]
    [DeploymentItem("Test2.ruleset")]
    public class RuleSetTests
    {
        /// <summary>
        /// Gets or sets the test context.
        /// </summary>
        public TestContext? TestContext { get; set; }

        /// <summary>
        /// Unit test for the <see cref="RuleSet.Evaluate(RuleEvaluationContext)"/> method.
        /// </summary>
        [TestMethod]
        public void EvaluateTest()
        {
            using (RuleSet target = new RuleSet())
            {
                MockMessageWriter messageWriter = new MockMessageWriter();
                RuleEvaluationContext context = new RuleEvaluationContext(
                    new ThreatModelForge.Model.ThreatModel(),
                    messageWriter);
                target.Evaluate(context);
                Assert.AreEqual(0, messageWriter.Messages.Count);

                target.Rules.Add(new Rule1());
                target.Rules.Add(new Rule2());
                target.Evaluate(context);
                Assert.AreEqual(2, messageWriter.Messages.Count);
                Assert.IsTrue(messageWriter.Messages.Any(e => string.Equals(e.Text, "Rule1", StringComparison.Ordinal)));
                Assert.IsTrue(messageWriter.Messages.Any(e => string.Equals(e.Text, "Rule2", StringComparison.Ordinal)));
            }
        }

        /// <summary>
        /// Unit test for the <see cref="RuleSet.Load(string, IEnumerable{System.Reflection.Assembly})"/> method.
        /// </summary>
        [TestMethod]
        public void LoadTest()
        {
            Assert.IsNotNull(this.TestContext!.DeploymentDirectory);
            string path = System.IO.Path.Combine(
                this.TestContext!.DeploymentDirectory,
                "Test2.ruleset");
            RuleSet target = RuleSet.Load(path, new System.Reflection.Assembly[] { this.GetType().Assembly });
            Assert.IsNotNull(target);

            // rule 2 is disabled.
            Assert.AreEqual(2, target.Rules.Count);
            Rule? actual1 = target.Rules.FirstOrDefault(e => string.Equals(e.ID, "TM1234"));
            Assert.IsNotNull(actual1);
            Assert.AreEqual("TM1234", actual1!.ID);
            Assert.AreEqual(MessageSeverity.Info, actual1.Severity);
            Rule? actual2 = target.Rules.FirstOrDefault(e => string.Equals(e.ID, "TM1235"));
            Assert.IsNotNull(actual2);
            Assert.AreEqual(true, actual2!.Disabled);
        }

        /// <summary>
        /// Unit test for the <see cref="RuleSet.Dispose()"/> method.
        /// </summary>
        [TestMethod]
        public void DisposeTest()
        {
            Rule1 rule = new Rule1();
            using (RuleSet target = new RuleSet())
            {
                target.Rules.Add(rule);
            }

            Assert.IsTrue(rule.IsDisposed);
        }

        /// <summary>
        /// Disabling a rule pack skips every rule in that pack during evaluation.
        /// </summary>
        [TestMethod]
        public void DisableByPackTest()
        {
            using (RuleSet target = new RuleSet())
            {
                target.Rules.Add(new Rule1());
                target.Rules.Add(new Rule2());
                target.Disable(new[] { "Test" }, null);

                MockMessageWriter writer = new MockMessageWriter();
                target.Evaluate(new RuleEvaluationContext(new ThreatModelForge.Model.ThreatModel(), writer));
                Assert.AreEqual(0, writer.Messages.Count);
            }
        }

        /// <summary>
        /// Disabling by rule id skips only the matching rule; matching is case-insensitive.
        /// </summary>
        [TestMethod]
        public void DisableByRuleIdTest()
        {
            using (RuleSet target = new RuleSet())
            {
                target.Rules.Add(new Rule1());
                target.Rules.Add(new Rule2());
                target.Disable(null, new[] { "tm1234" });

                MockMessageWriter writer = new MockMessageWriter();
                target.Evaluate(new RuleEvaluationContext(new ThreatModelForge.Model.ThreatModel(), writer));
                Assert.AreEqual(1, writer.Messages.Count);
                Assert.IsTrue(writer.Messages.Any(e => string.Equals(e.Text, "Rule2", StringComparison.Ordinal)));
            }
        }

        /// <summary>
        /// A selection that matches no pack or id leaves every rule enabled.
        /// </summary>
        [TestMethod]
        public void DisableWithNoMatchesTest()
        {
            using (RuleSet target = new RuleSet())
            {
                target.Rules.Add(new Rule1());
                target.Rules.Add(new Rule2());
                target.Disable(new[] { "does-not-exist" }, new[] { "TM9999" });

                MockMessageWriter writer = new MockMessageWriter();
                target.Evaluate(new RuleEvaluationContext(new ThreatModelForge.Model.ThreatModel(), writer));
                Assert.AreEqual(2, writer.Messages.Count);
            }
        }
    }
}
