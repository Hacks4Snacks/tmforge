namespace ThreatModelForge.Analysis.Rules.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the <see cref="EdgeMissingProtocolDescriptionRule"/> class.
    /// </summary>
    [TestClass]
    public class EdgeMissingProtocolDescriptionRuleTests
    {
        /// <summary>
        /// Unit test for the <see cref="EdgeMissingProtocolDescriptionRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using var target = new EdgeMissingProtocolDescriptionRule();
            Assert.AreEqual($"TM{RuleIDs.EdgeMissingProtocolDescriptionRule}", target.ID);
            Assert.AreEqual(MessageSeverity.Info, target.Severity);
        }

        /// <summary>
        /// Unit test for the <see cref="EdgeMissingProtocolDescriptionRule.Evaluate(RuleEvaluationContext)"/> method.
        /// </summary>
        [TestMethod]
        public void EvaluateTest()
        {
            Connector line1 = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceGuid = Guid.Empty,
                TargetGuid = Guid.Empty,
                Properties =
                {
                    new StringDisplayAttribute { DisplayName = "Name", Value = "Missing proto desc." },
                    new HeaderDisplayAttribute { DisplayName = "HTTP", Value = "HTTP" },
                },
            };

            ThreatModel model = new ThreatModel()
            {
                DrawingSurfaceList =
                {
                    new DrawingSurfaceModel
                    {
                        Lines =
                        {
                            { line1.Guid, line1 },
                        },
                    },
                },
            };

            using var target = new EdgeMissingProtocolDescriptionRule();
            MockMessageWriter writer = new MockMessageWriter();
            var context = new RuleEvaluationContext(model, writer);
            target.Evaluate(context);
            Assert.AreEqual(1, writer.Messages.Count);
            Message actual = writer.Messages[0];
            Assert.AreEqual(target.ID, actual.Source!.ID);
            Assert.AreEqual(line1.Guid, actual.Target!.Guid);
            Assert.IsFalse(string.IsNullOrWhiteSpace(actual.Text));
        }

        /// <summary>
        /// Unit test for the <see cref="EdgeMissingProtocolDescriptionRule.Evaluate(RuleEvaluationContext)"/> method.
        /// </summary>
        [TestMethod]
        public void EvaluateCustomProtocolInNameTest()
        {
            Connector line1 = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceGuid = Guid.Empty,
                TargetGuid = Guid.Empty,
                Properties =
                {
                    new StringDisplayAttribute { DisplayName = "Name", Value = "Foo\nProtocol:Foo" },
                    new HeaderDisplayAttribute { DisplayName = "Foo", Value = "Foo" },
                },
            };

            ThreatModel model = new ThreatModel()
            {
                DrawingSurfaceList =
                {
                    new DrawingSurfaceModel
                    {
                        Lines =
                        {
                            { line1.Guid, line1 },
                        },
                    },
                },
            };

            using var target = new EdgeMissingProtocolDescriptionRule();
            MockMessageWriter writer = new MockMessageWriter();
            var context = new RuleEvaluationContext(model, writer);
            target.Evaluate(context);
            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// Unit test for the <see cref="EdgeMissingProtocolDescriptionRule.Evaluate(RuleEvaluationContext)"/> method.
        /// </summary>
        [TestMethod]
        public void EvaluateExplicitProtocolInNameTest()
        {
            Connector line1 = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceGuid = Guid.Empty,
                TargetGuid = Guid.Empty,
                Properties =
                {
                    new StringDisplayAttribute { DisplayName = "Name", Value = "Foo\nProtocol:HTTP" },
                    new HeaderDisplayAttribute { DisplayName = "Foo", Value = "Foo" },
                },
            };

            ThreatModel model = new ThreatModel()
            {
                DrawingSurfaceList =
                {
                    new DrawingSurfaceModel
                    {
                        Lines =
                        {
                            { line1.Guid, line1 },
                        },
                    },
                },
            };

            using var target = new EdgeMissingProtocolDescriptionRule();
            MockMessageWriter writer = new MockMessageWriter();
            var context = new RuleEvaluationContext(model, writer);
            target.Evaluate(context);
            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// Unit test for the <see cref="EdgeMissingProtocolDescriptionRule.Evaluate(RuleEvaluationContext)"/> method.
        /// </summary>
        [TestMethod]
        public void EvaluateImplicitProtocolInNameTest()
        {
            Connector line1 = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceGuid = Guid.Empty,
                TargetGuid = Guid.Empty,
                Properties =
                {
                    new StringDisplayAttribute { DisplayName = "Name", Value = "Foo (HTTP)" },
                    new HeaderDisplayAttribute { DisplayName = "Foo", Value = "Foo" },
                },
            };

            ThreatModel model = new ThreatModel()
            {
                DrawingSurfaceList =
                {
                    new DrawingSurfaceModel
                    {
                        Lines =
                        {
                            { line1.Guid, line1 },
                        },
                    },
                },
            };

            using var target = new EdgeMissingProtocolDescriptionRule();
            MockMessageWriter writer = new MockMessageWriter();
            var context = new RuleEvaluationContext(model, writer);
            target.Evaluate(context);
            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// The modern protocols the flow schema recommends (gRPC, mTLS, TLS, AMQP, SQL) are
        /// recognized in the free-form edge label text, so an edge that names one draws no finding.
        /// </summary>
        [TestMethod]
        public void EvaluateModernProtocolInNameTest()
        {
            foreach (string protocol in new[] { "gRPC", "mTLS", "TLS", "AMQP", "SQL" })
            {
                Connector line = new Connector
                {
                    Guid = Guid.NewGuid(),
                    SourceGuid = Guid.Empty,
                    TargetGuid = Guid.Empty,
                    Properties =
                    {
                        new StringDisplayAttribute { DisplayName = "Name", Value = $"Sync ({protocol})" },
                        new HeaderDisplayAttribute { DisplayName = "Flow", Value = "Flow" },
                    },
                };

                ThreatModel model = new ThreatModel()
                {
                    DrawingSurfaceList =
                    {
                        new DrawingSurfaceModel
                        {
                            Lines =
                            {
                                { line.Guid, line },
                            },
                        },
                    },
                };

                using var target = new EdgeMissingProtocolDescriptionRule();
                MockMessageWriter writer = new MockMessageWriter();
                target.Evaluate(new RuleEvaluationContext(model, writer));
                Assert.AreEqual(0, writer.Messages.Count, $"{protocol} should be recognized in the edge label text");
            }
        }

        /// <summary>
        /// A protocol declared only as a property (not named in the label) also clears the
        /// description finding, so declaring Protocol=gRPC once satisfies the protocol rules together.
        /// </summary>
        [TestMethod]
        public void EvaluateProtocolPropertyClearsFindingTest()
        {
            Connector line1 = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceGuid = Guid.Empty,
                TargetGuid = Guid.Empty,
                Properties =
                {
                    new StringDisplayAttribute { DisplayName = "Name", Value = "Sync" },
                    new HeaderDisplayAttribute { DisplayName = "Flow", Value = "Flow" },
                    new CustomStringDisplayAttribute { Value = "Protocol:gRPC" },
                },
            };

            ThreatModel model = new ThreatModel()
            {
                DrawingSurfaceList =
                {
                    new DrawingSurfaceModel
                    {
                        Lines =
                        {
                            { line1.Guid, line1 },
                        },
                    },
                },
            };

            using var target = new EdgeMissingProtocolDescriptionRule();
            MockMessageWriter writer = new MockMessageWriter();
            target.Evaluate(new RuleEvaluationContext(model, writer));
            Assert.AreEqual(0, writer.Messages.Count);
        }
    }
}
