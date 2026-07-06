namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit test for the <see cref="OutboundStorageEdgeRule"/> class.
    /// </summary>
    [TestClass]
    public class OutboundStorageEdgeRuleTests
    {
        /// <summary>
        /// Gets or sets the test context.
        /// </summary>
        public TestContext? TestContext { get; set; }

        /// <summary>
        /// Unit test for the <see cref="OutboundStorageEdgeRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using (OutboundStorageEdgeRule target = new OutboundStorageEdgeRule())
            {
                Assert.AreEqual("TM1007", target.ID);
                Assert.AreEqual(MessageSeverity.Warning, target.Severity);
                Assert.IsNotNull(target.HelpUri);
                Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
                Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
            }
        }

        /// <summary>
        /// Unit test that shows the rule ignores non storage components.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoreNonStorageComponentsTest()
        {
            // A non-storage component with more outbound than inbound edges must NOT be flagged.
            StencilEllipse process = new StencilEllipse
            {
                Guid = Guid.NewGuid(),
                TypeId = "GE.P",
                GenericTypeId = "GE.P",
                Properties =
                {
                    new HeaderDisplayAttribute { Name = "Generic Process", DisplayName = "Generic Process" },
                    new StringDisplayAttribute { Name = "Name", DisplayName = "Name", Value = "Web App" },
                },
            };
            StencilEllipse other = new StencilEllipse
            {
                Guid = Guid.NewGuid(),
                TypeId = "GE.P",
                GenericTypeId = "GE.P",
                Properties =
                {
                    new HeaderDisplayAttribute { Name = "Generic Process", DisplayName = "Generic Process" },
                    new StringDisplayAttribute { Name = "Name", DisplayName = "Name", Value = "Service" },
                },
            };
            Connector edge = new Connector
            {
                Guid = Guid.NewGuid(),
                TypeId = "GE.DF",
                GenericTypeId = "GE.DF",
                SourceGuid = process.Guid,
                TargetGuid = other.Guid,
            };
            ThreatModel model = new ThreatModel
            {
                DrawingSurfaceList =
                {
                    new DrawingSurfaceModel
                    {
                        Header = "DFD-0",
                        Borders =
                        {
                            { process.Guid, process },
                            { other.Guid, other },
                        },
                        Lines =
                        {
                            { edge.Guid, edge },
                        },
                    },
                },
            };

            MockMessageWriter mockWriter = new MockMessageWriter();
            using (OutboundStorageEdgeRule target = new OutboundStorageEdgeRule())
            {
                RuleEvaluationContext context = new RuleEvaluationContext(model, mockWriter);
                target.Evaluate(context);
            }

            Assert.AreEqual(0, mockWriter.Messages.Count);
        }

        /// <summary>
        /// Unit test that shows the rule is violated for a single outbound connection.
        /// </summary>
        [TestMethod]
        public void EvaluateSingleViolationTest()
        {
            // A storage component with one outbound edge and no inbound edge is a single violation.
            StencilParallelLines storage = new StencilParallelLines
            {
                Guid = Guid.NewGuid(),
                TypeId = "SE.DS.TMCore.SQL",
                GenericTypeId = "GE.DS",
                Properties =
                {
                    new HeaderDisplayAttribute { Name = "SQL Database", DisplayName = "SQL Database" },
                    new StringDisplayAttribute { Name = "Name", DisplayName = "Name", Value = "Azure SQL Database" },
                },
            };
            StencilEllipse consumer = new StencilEllipse
            {
                Guid = Guid.NewGuid(),
                TypeId = "GE.P",
                GenericTypeId = "GE.P",
                Properties =
                {
                    new HeaderDisplayAttribute { Name = "Generic Process", DisplayName = "Generic Process" },
                    new StringDisplayAttribute { Name = "Name", DisplayName = "Name", Value = "Reader" },
                },
            };
            Connector outbound = new Connector
            {
                Guid = Guid.NewGuid(),
                TypeId = "GE.DF",
                GenericTypeId = "GE.DF",
                SourceGuid = storage.Guid,
                TargetGuid = consumer.Guid,
            };
            ThreatModel model = new ThreatModel
            {
                DrawingSurfaceList =
                {
                    new DrawingSurfaceModel
                    {
                        Header = "DFD-0",
                        Borders =
                        {
                            { storage.Guid, storage },
                            { consumer.Guid, consumer },
                        },
                        Lines =
                        {
                            { outbound.Guid, outbound },
                        },
                    },
                },
            };

            MockMessageWriter mockWriter = new MockMessageWriter();
            using (OutboundStorageEdgeRule target = new OutboundStorageEdgeRule())
            {
                RuleEvaluationContext context = new RuleEvaluationContext(model, mockWriter);
                target.Evaluate(context);
            }

            Assert.AreEqual(1, mockWriter.Messages.Count);
            Message actual = mockWriter.Messages[0];
            Assert.IsNotNull(actual.Source);
            Assert.IsNotNull(actual.Target);
            Assert.AreEqual("SE.DS.TMCore.SQL", actual.Target!.TypeId);
            Assert.IsNotNull(actual.Text);
            Assert.IsTrue(actual.Text!.Contains("(0)"));
            Assert.IsTrue(actual.Text!.Contains("(1)"));
            Assert.IsTrue(actual.Text!.Contains("SQL"));
        }
    }
}
