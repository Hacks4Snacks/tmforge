namespace ThreatModelForge.Analysis.Rules.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the <see cref="EdgeMissingProtocolRule"/> class.
    /// </summary>
    [TestClass]
    public class EdgeMissingProtocolRuleTests
    {
        /// <summary>
        /// Unit test for the <see cref="EdgeMissingProtocolRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using var target = new EdgeMissingProtocolRule();
            Assert.AreEqual($"TM{RuleIDs.EdgeMissingProtocolRule}", target.ID);
            Assert.AreEqual(MessageSeverity.Error, target.Severity);
        }

        /// <summary>
        /// Unit test for the <see cref="EdgeMissingProtocolRule.Evaluate(RuleEvaluationContext)"/> method.
        /// </summary>
        [TestMethod]
        public void EvaluateStencilTypeTest()
        {
            Connector line1 = new ()
            {
                Guid = Guid.NewGuid(),
                SourceGuid = Guid.Empty,
                TargetGuid = Guid.Empty,
                Properties =
                {
                    new StringDisplayAttribute { DisplayName = "Name", Value = "HTTP" },
                    new HeaderDisplayAttribute { DisplayName = "HTTP", Value = "HTTP" },
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
                    },
                },
            };

            using var target = new EdgeMissingProtocolRule();
            MockMessageWriter writer = new ();
            var context = new RuleEvaluationContext(model, writer);
            target.Evaluate(context);
            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// Unit test for the <see cref="EdgeMissingProtocolRule.Evaluate(RuleEvaluationContext)"/> method.
        /// </summary>
        [TestMethod]
        public void EvaluateCustomAttributeTest()
        {
            Connector line1 = new ()
            {
                Guid = Guid.NewGuid(),
                SourceGuid = Guid.Empty,
                TargetGuid = Guid.Empty,
                Properties =
                {
                    new StringDisplayAttribute { DisplayName = "Name", Value = "Foo" },
                    new HeaderDisplayAttribute { DisplayName = "Foo", Value = "Foo" },
                    new CustomStringDisplayAttribute { DisplayName = "Attr1", Value = "Protocol:Foo" },
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
                    },
                },
            };

            using var target = new EdgeMissingProtocolRule();
            MockMessageWriter writer = new ();
            var context = new RuleEvaluationContext(model, writer);
            target.Evaluate(context);
            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// Unit test for the <see cref="EdgeMissingProtocolRule.Evaluate(RuleEvaluationContext)"/> method.
        /// </summary>
        [TestMethod]
        public void EvaluateCustomProtocolInNameTest()
        {
            Connector line1 = new ()
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
                    },
                },
            };

            using var target = new EdgeMissingProtocolRule();
            MockMessageWriter writer = new ();
            var context = new RuleEvaluationContext(model, writer);
            target.Evaluate(context);
            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// Unit test for the <see cref="EdgeMissingProtocolRule.Evaluate(RuleEvaluationContext)"/> method.
        /// </summary>
        [TestMethod]
        public void EvaluateExplicitProtocolInNameTest()
        {
            Connector line1 = new ()
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
                    },
                },
            };

            using var target = new EdgeMissingProtocolRule();
            MockMessageWriter writer = new ();
            var context = new RuleEvaluationContext(model, writer);
            target.Evaluate(context);
            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// Unit test for the <see cref="EdgeMissingProtocolRule.Evaluate(RuleEvaluationContext)"/> method.
        /// </summary>
        [TestMethod]
        public void EvaluateImplicitProtocolInNameTest()
        {
            Connector line1 = new ()
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
                    },
                },
            };

            using var target = new EdgeMissingProtocolRule();
            MockMessageWriter writer = new ();
            var context = new RuleEvaluationContext(model, writer);
            target.Evaluate(context);
            Assert.AreEqual(0, writer.Messages.Count);
        }
    }
}
