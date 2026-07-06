namespace ThreatModelForge.Analysis.Rules.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the <see cref="DescriptiveGenericComponentNameRule"/> class.
    /// </summary>
    [TestClass]
    public class DescriptiveGenericComponentNameRuleTests
    {
        /// <summary>
        /// Unit test for the <see cref="DescriptiveGenericComponentNameRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using var target = new DescriptiveGenericComponentNameRule();
            Assert.AreEqual($"TM{RuleIDs.DescriptiveGenericComponentNameRule}", target.ID);
            Assert.AreEqual(MessageSeverity.Error, target.Severity);
        }

        /// <summary>
        /// Unit test for the <see cref="DescriptiveGenericComponentNameRule.Evaluate(RuleEvaluationContext)"/> method.
        /// </summary>
        [TestMethod]
        public void EvaluateTest()
        {
            StencilRectangle b1 = new StencilRectangle
            {
                Guid = Guid.NewGuid(),
                TypeId = "GE.EI",
                GenericTypeId = "GE.EI",
                Properties =
                {
                    new HeaderDisplayAttribute()
                    {
                        Name = "Generic Component",
                        DisplayName = "Generic Component",
                    },
                    new StringDisplayAttribute()
                    {
                        Name = "Name",
                        DisplayName = "Name",
                        Value = "Generic Component",
                    },
                },
            };

            ThreatModel model = new ThreatModel()
            {
                DrawingSurfaceList =
                {
                    new DrawingSurfaceModel
                    {
                        Borders =
                        {
                            { b1.Guid, b1 },
                        },
                    },
                },
            };

            using var target = new DescriptiveGenericComponentNameRule();
            MockMessageWriter writer = new MockMessageWriter();
            RuleEvaluationContext context = new RuleEvaluationContext(model, writer);
            target.Evaluate(context);
            Assert.AreEqual(1, writer.Messages.Count);
            Message actual = writer.Messages[0];
            Assert.AreEqual(target.ID, actual.Source!.ID);
            Assert.AreEqual(b1.Guid, actual.Target!.Guid);
            Assert.IsFalse(string.IsNullOrWhiteSpace(actual.Text));
        }
    }
}
