namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the <see cref="DescriptiveEdgeNameRule"/> class.
    /// </summary>
    [TestClass]
    public class DescriptiveEdgeNameRuleTests
    {
        /// <summary>
        /// Unit test for the <see cref="DescriptiveEdgeNameRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using (DescriptiveEdgeNameRule target = new DescriptiveEdgeNameRule())
            {
                Assert.AreEqual("TM1002", target.ID);
                Assert.AreEqual(MessageSeverity.Warning, target.Severity);
                Assert.IsNotNull(target.HelpUri);
                Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
                Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
            }
        }

        /// <summary>
        /// Unit test for the <see cref="DescriptiveEdgeNameRule.Evaluate(RuleEvaluationContext)"/> method.
        /// </summary>
        [TestMethod]
        public void EvaluateTest()
        {
            using (DescriptiveEdgeNameRule target = new DescriptiveEdgeNameRule())
            {
                MockMessageWriter mockWriter = new MockMessageWriter();
                StencilEllipse shape1 = new StencilEllipse
                {
                    Guid = Guid.NewGuid(),
                    Properties =
                {
                    new StringDisplayAttribute { DisplayName = "Name", Value = "S1" },
                },
                };

                StencilEllipse shape2 = new StencilEllipse
                {
                    Guid = Guid.NewGuid(),
                    Properties =
                {
                    new StringDisplayAttribute { DisplayName = "Name", Value = "S2" },
                },
                };

                Connector line1 = new Connector
                {
                    Guid = Guid.NewGuid(),
                    SourceGuid = shape1.Guid,
                    TargetGuid = shape2.Guid,
                    Properties =
                {
                    new StringDisplayAttribute { DisplayName = "Name", Value = "HTTP" },
                    new HeaderDisplayAttribute { DisplayName = "HTTP", Value = "HTTP" },
                },
                };

                ThreatModel model = new ThreatModel()
                {
                    DrawingSurfaceList =
                {
                    new DrawingSurfaceModel
                    {
                        Header = "M1",
                        Borders =
                        {
                            { shape1.Guid, shape1 },
                            { shape2.Guid, shape2 },
                        },
                        Lines =
                        {
                            { line1.Guid, line1 },
                        },
                    },
                },
                };

                RuleEvaluationContext context = new RuleEvaluationContext(model, mockWriter);
                target.Evaluate(context);
                Assert.AreEqual(1, mockWriter.Messages.Count);
                Message actual = mockWriter.Messages[0];
                Assert.AreEqual(target.Severity, actual.Severity);
                Assert.AreEqual(target, actual.Source);
                Assert.AreEqual(model.DrawingSurfaceList[0], actual.Model);
                Assert.AreEqual(line1, actual.Target);
                Assert.IsNotNull(actual.Text);
                Assert.IsTrue(actual.Text!.Contains("HTTP"));
            }
        }
    }
}
