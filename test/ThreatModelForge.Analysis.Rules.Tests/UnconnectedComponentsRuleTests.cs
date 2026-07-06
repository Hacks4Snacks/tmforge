namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit test for the <see cref="UnconnectedComponentsRule"/> class.
    /// </summary>
    [TestClass]
    public class UnconnectedComponentsRuleTests
    {
        /// <summary>
        /// Unit test for the <see cref="UnconnectedComponentsRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using (UnconnectedComponentsRule target = new UnconnectedComponentsRule())
            {
                Assert.AreEqual("TM1000", target.ID);
                Assert.AreEqual(MessageSeverity.Error, target.Severity);
                Assert.IsNotNull(target.HelpUri);
                Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
                Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
            }
        }

        /// <summary>
        /// Unit test that has no errors for connected components.
        /// </summary>
        [TestMethod]
        public void EvaluateConnectedComponentsTest()
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
                    new StringDisplayAttribute { DisplayName = "Name", Value = "L1" },
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
            using (UnconnectedComponentsRule target = new UnconnectedComponentsRule())
            {
                target.Evaluate(context);
                Assert.AreEqual(0, mockWriter.Messages.Count);
            }
        }

        /// <summary>
        /// Unit test for an unconnected component.
        /// </summary>
        [TestMethod]
        public void EvaluateUnconnectedComponentTest()
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
                        },
                    },
                    new DrawingSurfaceModel
                    {
                        Header = "M2",
                        Borders =
                        {
                            { shape2.Guid, shape2 },
                        },
                    },
                },
            };

            RuleEvaluationContext context = new RuleEvaluationContext(model, mockWriter);
            using (UnconnectedComponentsRule target = new UnconnectedComponentsRule())
            {
                target.Evaluate(context);
                Assert.AreEqual(2, mockWriter.Messages.Count);
                Message actual1 = mockWriter.Messages[0];
                Assert.IsNotNull(actual1.Model);
                Assert.AreEqual("M1", actual1.Model!.Header);
                Assert.IsNotNull(actual1.Source);
                Assert.IsNotNull(actual1.Target);
                Assert.AreEqual(shape1.Guid, actual1.Target!.Guid);
                Assert.IsFalse(string.IsNullOrEmpty(actual1.Text));

                Message actual2 = mockWriter.Messages[1];
                Assert.IsNotNull(actual2.Model);
                Assert.AreEqual("M2", actual2.Model!.Header);
                Assert.IsNotNull(actual2.Source);
                Assert.IsNotNull(actual2.Target);
                Assert.AreEqual(shape2.Guid, actual2.Target!.Guid);
                Assert.IsFalse(string.IsNullOrEmpty(actual2.Text));
            }
        }
    }
}
