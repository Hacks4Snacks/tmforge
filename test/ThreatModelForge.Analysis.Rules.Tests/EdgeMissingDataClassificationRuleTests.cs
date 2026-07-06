namespace ThreatModelForge.Analysis.Rules.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the <see cref="EdgeMissingDataClassificationRule"/> class.
    /// </summary>
    [TestClass]
    public class EdgeMissingDataClassificationRuleTests
    {
        /// <summary>
        /// Unit test for the <see cref="EdgeMissingDataClassificationRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using var target = new EdgeMissingDataClassificationRule();
            Assert.AreEqual($"TM{RuleIDs.EdgeMissingDataClassificationRule}", target.ID);
            Assert.AreEqual(MessageSeverity.Warning, target.Severity);
            Assert.IsNotNull(target.HelpUri);
            Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
            Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
        }

        /// <summary>
        /// Unit test for the <see cref="EdgeMissingDataClassificationRule.Evaluate(RuleEvaluationContext)"/> method.
        /// </summary>
        [TestMethod]
        public void EvaulateDiagramWithLegendTest()
        {
            var line1 = new Connector
            {
                Guid = Guid.NewGuid(),
            };

            var text = new StencilRectangle()
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = "GE.A",
                TypeId = "GE.A",
                Properties =
                {
                    new StringDisplayAttribute { DisplayName = "Name", Value = "Unless otherwise noted, all data is System Metadata." },
                },
            };

            ThreatModel model = new ()
            {
                DrawingSurfaceList =
                {
                    new DrawingSurfaceModel
                    {
                        Lines =
                        {
                            { line1.Guid, line1 },
                        },
                        Borders =
                        {
                            { text.Guid, text },
                        },
                    },
                },
            };

            var writer = new MockMessageWriter();
            var context = new RuleEvaluationContext(model, writer);
            using var target = new EdgeMissingDataClassificationRule();
            target.Evaluate(context);
            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// Unit test for the <see cref="EdgeMissingDataClassificationRule.Evaluate(RuleEvaluationContext)"/> method.
        /// </summary>
        [TestMethod]
        public void EvaulateMissingTypeTest()
        {
            var line1 = new Connector
            {
                Guid = Guid.NewGuid(),
                Properties =
                {
                    new StringDisplayAttribute { DisplayName = "Name", Value = "Missing data classification." },
                },
            };

            var line2 = new Connector
            {
                Guid = Guid.NewGuid(),
                Properties =
                {
                    new StringDisplayAttribute { DisplayName = "Name", Value = "Bifrost bridge for worthy souls entering Valhalla. Customer Content." },
                },
            };

            var model = new ThreatModel()
            {
                DrawingSurfaceList =
                {
                    new DrawingSurfaceModel
                    {
                        Lines =
                        {
                            { line1.Guid, line1 },
                            { line2.Guid, line2 },
                        },
                    },
                },
            };

            var writer = new MockMessageWriter();
            var context = new RuleEvaluationContext(model, writer);
            using var target = new EdgeMissingDataClassificationRule();
            target.Evaluate(context);
            Assert.AreEqual(1, writer.Messages.Count);
        }
    }
}
