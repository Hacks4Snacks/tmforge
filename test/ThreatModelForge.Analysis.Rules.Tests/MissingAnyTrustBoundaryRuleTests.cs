namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the <see cref="MissingAnyTrustBoundaryRule"/> class.
    /// </summary>
    [TestClass]
    public class MissingAnyTrustBoundaryRuleTests
    {
        /// <summary>
        /// Unit test for the <see cref="MissingAnyTrustBoundaryRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using (MissingAnyTrustBoundaryRule target = new MissingAnyTrustBoundaryRule())
            {
                Assert.IsNotNull(target.ID);
                Assert.AreEqual(MessageSeverity.Error, target.Severity);
                Assert.IsNotNull(target.HelpUri);
                Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
                Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
            }
        }

        /// <summary>
        /// Unit test for the <see cref="MissingAnyTrustBoundaryRule.Evaluate(RuleEvaluationContext)"/> method.
        /// </summary>
        [TestMethod]
        public void EvaluateTest()
        {
            using (MissingAnyTrustBoundaryRule target = new MissingAnyTrustBoundaryRule())
            {
                BorderBoundary b1 = new BorderBoundary { Guid = Guid.NewGuid() };

                LineBoundary l1 = new LineBoundary { Guid = Guid.NewGuid() };

                StencilEllipse e1 = new StencilEllipse { Guid = Guid.NewGuid() };
                StencilEllipse e2 = new StencilEllipse { Guid = Guid.NewGuid() };
                StencilEllipse e3 = new StencilEllipse { Guid = Guid.NewGuid() };

                ThreatModel model = new ThreatModel
                {
                    DrawingSurfaceList =
                {
                    new DrawingSurfaceModel
                    {
                        Header = "DFD-0",
                        Borders =
                        {
                            { b1.Guid, b1 },
                            { e1.Guid, e1 },
                        },
                    },
                    new DrawingSurfaceModel
                    {
                        Header = "DFD-1",
                        Borders =
                        {
                            { e2.Guid, e2 },
                            { e3.Guid, e3 },
                        },
                    },
                },
                };

                MockMessageWriter writer = new MockMessageWriter();
                RuleEvaluationContext context = new RuleEvaluationContext(model, writer);
                target.Evaluate(context);
                Assert.AreEqual(0, writer.Messages.Count);

                // try with a line instead.
                model.DrawingSurfaceList[0].Borders.Remove(b1.Guid);
                model.DrawingSurfaceList[1].Lines.Add(l1.Guid, l1);
                target.Evaluate(context);
                Assert.AreEqual(0, writer.Messages.Count);

                // show the rule working.
                model.DrawingSurfaceList[1].Lines.Remove(l1.Guid);
                target.Evaluate(context);
                Assert.AreEqual(1, writer.Messages.Count);
            }
        }
    }
}
