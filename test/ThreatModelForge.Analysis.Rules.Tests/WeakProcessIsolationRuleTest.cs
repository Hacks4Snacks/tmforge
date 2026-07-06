namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the <see cref="WeakProcessIsolationRule"/> class.
    /// </summary>
    [TestClass]
    public class WeakProcessIsolationRuleTest
    {
        private const string ProcessGenericTypeId = "GE.P";

        /// <summary>
        /// Unit test for the <see cref="WeakProcessIsolationRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using (WeakProcessIsolationRule target = new WeakProcessIsolationRule())
            {
                Assert.AreEqual("TM1019", target.ID);
                Assert.AreEqual(MessageSeverity.Warning, target.Severity);
                Assert.IsNotNull(target.HelpUri);
                Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
                Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
            }
        }

        /// <summary>
        /// A process with no isolation that receives an inbound trust-boundary crossing is flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateFlagsUnisolatedInboundCrossingTest()
        {
            StencilEllipse process = CreateProcess("Order Service", "None");
            BorderBoundary border = CreateBoundary();
            Connector inbound = CreateCrossingInbound(process);
            ThreatModel model = BuildModel(border, process, inbound);
            MockMessageWriter writer = new MockMessageWriter();

            using (WeakProcessIsolationRule target = new WeakProcessIsolationRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(1, writer.Messages.Count);
            Assert.AreSame(process, writer.Messages[0].Target);
            Assert.IsTrue(writer.Messages[0].Text!.Contains("Order Service"));
        }

        /// <summary>
        /// A process that runs with meaningful isolation is not flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresIsolatedProcessTest()
        {
            StencilEllipse process = CreateProcess("Order Service", "Container");
            BorderBoundary border = CreateBoundary();
            Connector inbound = CreateCrossingInbound(process);
            ThreatModel model = BuildModel(border, process, inbound);
            MockMessageWriter writer = new MockMessageWriter();

            using (WeakProcessIsolationRule target = new WeakProcessIsolationRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// A process with no isolation whose inbound edge does not cross a trust boundary is not flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresNonCrossingInboundTest()
        {
            StencilEllipse process = CreateProcess("Order Service", "None");
            BorderBoundary border = CreateBoundary();
            Connector inbound = CreateNonCrossingInbound(process);
            ThreatModel model = BuildModel(border, process, inbound);
            MockMessageWriter writer = new MockMessageWriter();

            using (WeakProcessIsolationRule target = new WeakProcessIsolationRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        private static StencilEllipse CreateProcess(string name, string isolation)
        {
            StencilEllipse process = new StencilEllipse
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = ProcessGenericTypeId,
            };
            process.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = name });
            process.Properties.Add(new CustomStringDisplayAttribute { Value = $"Isolation:{isolation}" });
            return process;
        }

        private static BorderBoundary CreateBoundary()
        {
            return new BorderBoundary { Guid = Guid.NewGuid(), Left = 5, Top = 10, Height = 15, Width = 20 };
        }

        private static Connector CreateCrossingInbound(StencilEllipse process)
        {
            return new Connector
            {
                Guid = Guid.NewGuid(),
                SourceX = 0,
                SourceY = 0,
                TargetX = 10,
                TargetY = 11,
                TargetGuid = process.Guid,
            };
        }

        private static Connector CreateNonCrossingInbound(StencilEllipse process)
        {
            return new Connector
            {
                Guid = Guid.NewGuid(),
                SourceX = 8,
                SourceY = 12,
                TargetX = 10,
                TargetY = 11,
                TargetGuid = process.Guid,
            };
        }

        private static ThreatModel BuildModel(BorderBoundary border, StencilEllipse process, Connector connector)
        {
            return new ThreatModel
            {
                DrawingSurfaceList =
                {
                    new DrawingSurfaceModel
                    {
                        Header = "DFD-0",
                        Borders =
                        {
                            { border.Guid, border },
                            { process.Guid, process },
                        },
                        Lines =
                        {
                            { connector.Guid, connector },
                        },
                    },
                },
            };
        }
    }
}
