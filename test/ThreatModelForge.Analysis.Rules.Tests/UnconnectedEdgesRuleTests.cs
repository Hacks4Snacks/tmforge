namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the <see cref="UnconnectedEdgesRule"/> class.
    /// </summary>
    [TestClass]
    public class UnconnectedEdgesRuleTests
    {
        /// <summary>
        /// Unit test for the <see cref="UnconnectedEdgesRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using (UnconnectedEdgesRule target = new UnconnectedEdgesRule())
            {
                Assert.AreEqual("TM1001", target.ID);
                Assert.AreEqual(MessageSeverity.Error, target.Severity);
                Assert.IsNotNull(target.HelpUri);
                Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
                Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
            }
        }

        /// <summary>
        /// Unit test for the <see cref="UnconnectedEdgesRule.Evaluate(RuleEvaluationContext)"/> class.
        /// </summary>
        [TestMethod]
        public void EvaluateConnectedEdge()
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
            using (UnconnectedEdgesRule target = new UnconnectedEdgesRule())
            {
                target.Evaluate(context);
                Assert.AreEqual(0, mockWriter.Messages.Count);
            }
        }

        /// <summary>
        /// Unit test for the <see cref="UnconnectedEdgesRule.Evaluate(RuleEvaluationContext)"/> class.
        /// </summary>
        [TestMethod]
        public void EvaluateUnconnectedEdges()
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

            Connector line1 = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceGuid = shape1.Guid,
                TargetGuid = Guid.Empty,
                Properties =
                {
                    new StringDisplayAttribute { DisplayName = "Name", Value = "L1" },
                },
            };

            Connector line2 = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceGuid = Guid.Empty,
                TargetGuid = shape1.Guid,
                Properties =
                {
                    new StringDisplayAttribute { DisplayName = "Name", Value = "L2" },
                },
            };

            Connector line3 = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceGuid = Guid.Empty,
                TargetGuid = Guid.Empty,
                Properties =
                {
                    new StringDisplayAttribute { DisplayName = "Name", Value = "L3" },
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
                        Lines =
                        {
                            { line1.Guid, line1 },
                            { line2.Guid, line2 },
                        },
                    },
                    new DrawingSurfaceModel
                    {
                        Header = "M2",
                        Lines =
                        {
                            { line3.Guid, line3 },
                        },
                    },
                },
            };

            RuleEvaluationContext context = new RuleEvaluationContext(model, mockWriter);
            using (UnconnectedEdgesRule target = new UnconnectedEdgesRule())
            {
                target.Evaluate(context);
                Assert.AreEqual(3, mockWriter.Messages.Count);
                Message actual1 = mockWriter.Messages[0];
                Assert.IsNotNull(actual1.Model);
                Assert.AreEqual("M1", actual1.Model!.Header);
                Assert.IsNotNull(actual1.Source);
                Assert.IsNotNull(actual1.Target);
                Assert.AreEqual(line1.Guid, actual1.Target!.Guid);
                Assert.IsFalse(string.IsNullOrEmpty(actual1.Text));

                Message actual2 = mockWriter.Messages[1];
                Assert.IsNotNull(actual2.Model);
                Assert.AreEqual("M1", actual2.Model!.Header);
                Assert.IsNotNull(actual2.Source);
                Assert.IsNotNull(actual2.Target);
                Assert.AreEqual(line2.Guid, actual2.Target!.Guid);
                Assert.IsFalse(string.IsNullOrEmpty(actual2.Text));

                Message actual3 = mockWriter.Messages[2];
                Assert.IsNotNull(actual3.Model);
                Assert.AreEqual("M2", actual3.Model!.Header);
                Assert.IsNotNull(actual3.Source);
                Assert.IsNotNull(actual3.Target);
                Assert.AreEqual(line3.Guid, actual3.Target!.Guid);
                Assert.IsFalse(string.IsNullOrEmpty(actual3.Text));
            }
        }
    }
}
