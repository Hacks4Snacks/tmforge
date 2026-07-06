namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the <see cref="MissingAnyTrustBoundaryCrossingRule"/> class.
    /// </summary>
    [TestClass]
    public class MissingAnyTrustBoundaryCrossingRuleTests
    {
        /// <summary>
        /// Unit test for the <see cref="MissingAnyTrustBoundaryCrossingRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using (MissingAnyTrustBoundaryCrossingRule target = new MissingAnyTrustBoundaryCrossingRule())
            {
                Assert.IsNotNull(target.ID);
                Assert.AreEqual(MessageSeverity.Error, target.Severity);
                Assert.IsNotNull(target.HelpUri);
                Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
                Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
            }
        }

        /// <summary>
        /// Unit test for the <see cref="MissingAnyTrustBoundaryCrossingRule.Evaluate(RuleEvaluationContext)"/> method.
        /// </summary>
        [TestMethod]
        public void EvaluateTest()
        {
            using (MissingAnyTrustBoundaryCrossingRule target = new MissingAnyTrustBoundaryCrossingRule())
            {
                MockMessageWriter writer = new MockMessageWriter();

                BorderBoundary border = new BorderBoundary
                {
                    Guid = Guid.NewGuid(),
                    Left = 5,
                    Top = 10,
                    Height = 15,
                    Width = 20,
                };

                Connector inside = new Connector
                {
                    Guid = Guid.NewGuid(),
                    SourceX = 10,
                    SourceY = 11,
                    TargetX = 12,
                    TargetY = 15,
                };

                Connector crossesIn = new Connector
                {
                    Guid = Guid.NewGuid(),
                    SourceX = 0,
                    SourceY = 0,
                    TargetX = 10,
                    TargetY = 11,
                };

                Connector outside = new Connector
                {
                    Guid = Guid.NewGuid(),
                    SourceX = 50,
                    SourceY = 40,
                    TargetX = 100,
                    TargetY = 100,
                };

                ThreatModel model = new ThreatModel()
                {
                    DrawingSurfaceList =
                {
                    new DrawingSurfaceModel
                    {
                        Header = "DFD-0",
                        Borders =
                        {
                            { border.Guid, border },
                        },
                        Lines =
                        {
                            { inside.Guid, inside },
                            { outside.Guid, outside },
                            { crossesIn.Guid, crossesIn },
                        },
                    },
                },
                };

                RuleEvaluationContext context = new RuleEvaluationContext(
                    model,
                    writer);
                target.Evaluate(context);
                Assert.AreEqual(0, writer.Messages.Count);

                model.DrawingSurfaceList[0].Lines.Remove(crossesIn.Guid);
                target.Evaluate(context);
                Assert.AreEqual(1, writer.Messages.Count);
            }
        }
    }
}
