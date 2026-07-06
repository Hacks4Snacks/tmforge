namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the <see cref="UnauthenticatedBoundaryProcessRule"/> class.
    /// </summary>
    [TestClass]
    public class UnauthenticatedBoundaryProcessRuleTest
    {
        private const string ProcessGenericTypeId = "GE.P";

        /// <summary>
        /// Unit test for the <see cref="UnauthenticatedBoundaryProcessRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using (UnauthenticatedBoundaryProcessRule target = new UnauthenticatedBoundaryProcessRule())
            {
                Assert.AreEqual("TM1015", target.ID);
                Assert.AreEqual(MessageSeverity.Warning, target.Severity);
                Assert.IsNotNull(target.HelpUri);
                Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
                Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
            }
        }

        /// <summary>
        /// A process with no authentication scheme that receives an inbound flow crossing a trust boundary is flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateFlagsUnauthenticatedInboundCrossingTest()
        {
            StencilEllipse process = CreateProcess("Order Service", "None");
            BorderBoundary border = CreateBoundary();
            Connector inbound = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceX = 0,
                SourceY = 0,
                TargetX = 10,
                TargetY = 11,
                TargetGuid = process.Guid,
            };
            ThreatModel model = BuildModel(border, process, inbound);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnauthenticatedBoundaryProcessRule target = new UnauthenticatedBoundaryProcessRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(1, writer.Messages.Count);
            Message actual = writer.Messages[0];
            Assert.AreEqual(MessageSeverity.Warning, actual.Severity);
            Assert.AreSame(process, actual.Target);
            Assert.IsNotNull(actual.Text);
            Assert.IsTrue(actual.Text!.Contains("Order Service"));
        }

        /// <summary>
        /// A process that declares an authentication scheme is not flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresAuthenticatedProcessTest()
        {
            StencilEllipse process = CreateProcess("Order Service", "OAuth");
            BorderBoundary border = CreateBoundary();
            Connector inbound = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceX = 0,
                SourceY = 0,
                TargetX = 10,
                TargetY = 11,
                TargetGuid = process.Guid,
            };
            ThreatModel model = BuildModel(border, process, inbound);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnauthenticatedBoundaryProcessRule target = new UnauthenticatedBoundaryProcessRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// An unauthenticated process whose inbound flow does not cross a trust boundary is not flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresInboundThatDoesNotCrossBoundaryTest()
        {
            StencilEllipse process = CreateProcess("Order Service", "None");
            BorderBoundary border = CreateBoundary();
            Connector inbound = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceX = 10,
                SourceY = 11,
                TargetX = 12,
                TargetY = 15,
                TargetGuid = process.Guid,
            };
            ThreatModel model = BuildModel(border, process, inbound);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnauthenticatedBoundaryProcessRule target = new UnauthenticatedBoundaryProcessRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        private static StencilEllipse CreateProcess(string name, string authenticationScheme)
        {
            StencilEllipse process = new StencilEllipse
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = ProcessGenericTypeId,
            };
            process.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = name });
            process.Properties.Add(new CustomStringDisplayAttribute { Value = $"AuthenticationScheme:{authenticationScheme}" });
            return process;
        }

        private static BorderBoundary CreateBoundary()
        {
            return new BorderBoundary
            {
                Guid = Guid.NewGuid(),
                Left = 5,
                Top = 10,
                Height = 15,
                Width = 20,
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
