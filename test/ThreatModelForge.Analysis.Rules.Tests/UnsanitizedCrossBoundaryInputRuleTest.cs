namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the <see cref="UnsanitizedCrossBoundaryInputRule"/> class.
    /// </summary>
    [TestClass]
    public class UnsanitizedCrossBoundaryInputRuleTest
    {
        private const string ProcessGenericTypeId = "GE.P";

        /// <summary>
        /// Unit test for the <see cref="UnsanitizedCrossBoundaryInputRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using (UnsanitizedCrossBoundaryInputRule target = new UnsanitizedCrossBoundaryInputRule())
            {
                Assert.AreEqual("TM1017", target.ID);
                Assert.AreEqual(MessageSeverity.Warning, target.Severity);
                Assert.IsNotNull(target.HelpUri);
                Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
                Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
            }
        }

        /// <summary>
        /// A process that does not sanitize input and receives an inbound trust-boundary crossing is flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateFlagsUnsanitizedInboundCrossingTest()
        {
            StencilEllipse process = CreateProcess("Order Service", "No");
            BorderBoundary border = CreateBoundary();
            Connector inbound = CreateCrossingInbound(process);
            ThreatModel model = BuildModel(border, process, inbound);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnsanitizedCrossBoundaryInputRule target = new UnsanitizedCrossBoundaryInputRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(1, writer.Messages.Count);
            Assert.AreSame(process, writer.Messages[0].Target);
            Assert.IsTrue(writer.Messages[0].Text!.Contains("Order Service"));
        }

        /// <summary>
        /// A process that sanitizes its input is not flagged even when it receives a crossing.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresSanitizedProcessTest()
        {
            StencilEllipse process = CreateProcess("Order Service", "Yes");
            BorderBoundary border = CreateBoundary();
            Connector inbound = CreateCrossingInbound(process);
            ThreatModel model = BuildModel(border, process, inbound);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnsanitizedCrossBoundaryInputRule target = new UnsanitizedCrossBoundaryInputRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// An unsanitized process whose inbound edge does not cross a trust boundary is not flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresNonCrossingInboundTest()
        {
            StencilEllipse process = CreateProcess("Order Service", "No");
            BorderBoundary border = CreateBoundary();
            Connector inbound = CreateNonCrossingInbound(process);
            ThreatModel model = BuildModel(border, process, inbound);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnsanitizedCrossBoundaryInputRule target = new UnsanitizedCrossBoundaryInputRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        private static StencilEllipse CreateProcess(string name, string sanitizesInput)
        {
            StencilEllipse process = new StencilEllipse
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = ProcessGenericTypeId,
            };
            process.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = name });
            process.Properties.Add(new CustomStringDisplayAttribute { Value = $"SanitizesInput:{sanitizesInput}" });
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
