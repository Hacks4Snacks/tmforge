namespace ThreatModelForge.Analysis.Rules.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the <see cref="EdgeMissingPortRule"/> test.
    /// </summary>
    [TestClass]
    public class EdgeMissingPortRuleTests
    {
        /// <summary>
        /// Unit test for the <see cref="EdgeMissingPortRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using var target = new EdgeMissingPortRule();
            Assert.AreEqual($"TM{RuleIDs.EdgeMissingPortRule}", target.ID);
        }

        /// <summary>
        /// Unit test for the <see cref="EdgeMissingPortRule.Evaluate(RuleEvaluationContext)"/> method.
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

            using var target = new EdgeMissingPortRule();
            MockMessageWriter writer = new ();
            var context = new RuleEvaluationContext(model, writer);
            target.Evaluate(context);
            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// A <c>gRPC</c> edge (a modern protocol now in the default set) infers its port, so no
        /// TM1010 finding is raised even when no explicit <c>Port</c> is declared.
        /// </summary>
        [TestMethod]
        public void EvaluateModernProtocolInfersPortTest()
        {
            Connector line1 = new ()
            {
                Guid = Guid.NewGuid(),
                SourceGuid = Guid.Empty,
                TargetGuid = Guid.Empty,
                Properties =
                {
                    new StringDisplayAttribute { DisplayName = "Name", Value = "gRPC call" },
                    new HeaderDisplayAttribute { DisplayName = "Flow", Value = "Flow" },
                    new CustomStringDisplayAttribute { DisplayName = "Attr1", Value = "Protocol:gRPC" },
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

            using var target = new EdgeMissingPortRule();
            MockMessageWriter writer = new ();
            var context = new RuleEvaluationContext(model, writer);
            target.Evaluate(context);
            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// Unit test for the <see cref="EdgeMissingPortRule.Evaluate(RuleEvaluationContext)"/> method.
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
                    new StringDisplayAttribute { DisplayName = "Name", Value = "Https" },
                    new HeaderDisplayAttribute { DisplayName = "Foo", Value = "Foo" },
                    new CustomStringDisplayAttribute { DisplayName = "Attr1", Value = "Port:9000" },
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

            using var target = new EdgeMissingPortRule();
            MockMessageWriter writer = new ();
            var context = new RuleEvaluationContext(model, writer);
            target.Evaluate(context);
            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// Unit test for the <see cref="EdgeMissingPortRule.Evaluate(RuleEvaluationContext)"/> method.
        /// </summary>
        [TestMethod]
        public void EvaluateCustomPortInNameTest()
        {
            Connector line1 = new ()
            {
                Guid = Guid.NewGuid(),
                SourceGuid = Guid.Empty,
                TargetGuid = Guid.Empty,
                Properties =
                {
                    new StringDisplayAttribute { DisplayName = "Name", Value = "Foo\nProtocol:Foo\nPort:9000" },
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

            using var target = new EdgeMissingPortRule();
            MockMessageWriter writer = new ();
            var context = new RuleEvaluationContext(model, writer);
            target.Evaluate(context);
            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// Unit test for the <see cref="EdgeMissingPortRule.Evaluate(RuleEvaluationContext)"/> method.
        /// </summary>
        [TestMethod]
        public void EvaluateExplicitPortInNameTest()
        {
            Connector line1 = new ()
            {
                Guid = Guid.NewGuid(),
                SourceGuid = Guid.Empty,
                TargetGuid = Guid.Empty,
                Properties =
                {
                    new StringDisplayAttribute { DisplayName = "Name", Value = "Foo\nProtocol:HTTP\nPort:9001" },
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

            using var target = new EdgeMissingPortRule();
            MockMessageWriter writer = new ();
            var context = new RuleEvaluationContext(model, writer);
            target.Evaluate(context);
            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// Unit test for the <see cref="EdgeMissingPortRule.Evaluate(RuleEvaluationContext)"/> method.
        /// </summary>
        [TestMethod]
        public void EvaluateImplicitPortInNameTest()
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

            using var target = new EdgeMissingPortRule();
            MockMessageWriter writer = new ();
            var context = new RuleEvaluationContext(model, writer);
            target.Evaluate(context);
            Assert.AreEqual(0, writer.Messages.Count);
        }
    }
}
