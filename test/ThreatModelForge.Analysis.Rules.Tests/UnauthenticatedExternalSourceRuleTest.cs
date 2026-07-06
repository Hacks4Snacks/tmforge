namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for the <see cref="UnauthenticatedExternalSourceRule"/> class.
    /// </summary>
    [TestClass]
    public class UnauthenticatedExternalSourceRuleTest
    {
        private const string ProcessGenericTypeId = "GE.P";
        private const string ExternalInteractorGenericTypeId = "GE.EI";

        /// <summary>
        /// Unit test for the <see cref="UnauthenticatedExternalSourceRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using (UnauthenticatedExternalSourceRule target = new UnauthenticatedExternalSourceRule())
            {
                Assert.AreEqual("TM1023", target.ID);
                Assert.AreEqual(MessageSeverity.Warning, target.Severity);
                Assert.IsNotNull(target.HelpUri);
                Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
                Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
            }
        }

        /// <summary>
        /// An external entity that initiates a flow into the system without authenticating itself is flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateFlagsUnauthenticatedExternalSourceTest()
        {
            StencilRectangle external = CreateExternal("Partner API", "No");
            StencilEllipse process = CreateProcess("Order Service");
            Connector inbound = CreateFlow(external, process);
            ThreatModel model = BuildModel(external, process, inbound);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnauthenticatedExternalSourceRule target = new UnauthenticatedExternalSourceRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(1, writer.Messages.Count);
            Assert.AreSame(external, writer.Messages[0].Target);
            Assert.IsTrue(writer.Messages[0].Text!.Contains("Partner API"));
        }

        /// <summary>
        /// An external entity missing the AuthenticatesItself property is flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateFlagsExternalWithoutAuthenticatesPropertyTest()
        {
            StencilRectangle external = CreateExternal("Anonymous Client");
            StencilEllipse process = CreateProcess("Order Service");
            Connector inbound = CreateFlow(external, process);
            ThreatModel model = BuildModel(external, process, inbound);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnauthenticatedExternalSourceRule target = new UnauthenticatedExternalSourceRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(1, writer.Messages.Count);
        }

        /// <summary>
        /// An external entity that authenticates itself is not flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresAuthenticatedExternalSourceTest()
        {
            StencilRectangle external = CreateExternal("Trusted Partner", "Yes");
            StencilEllipse process = CreateProcess("Order Service");
            Connector inbound = CreateFlow(external, process);
            ThreatModel model = BuildModel(external, process, inbound);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnauthenticatedExternalSourceRule target = new UnauthenticatedExternalSourceRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// An external entity that only receives flows (a sink) is not flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresExternalSinkTest()
        {
            StencilRectangle external = CreateExternal("Browser", "No");
            StencilEllipse process = CreateProcess("Web Server");
            Connector outbound = CreateFlow(process, external);
            ThreatModel model = new ThreatModel
            {
                DrawingSurfaceList =
                {
                    new DrawingSurfaceModel
                    {
                        Header = "DFD-0",
                        Borders =
                        {
                            { external.Guid, external },
                            { process.Guid, process },
                        },
                        Lines =
                        {
                            { outbound.Guid, outbound },
                        },
                    },
                },
            };
            MockMessageWriter writer = new MockMessageWriter();

            using (UnauthenticatedExternalSourceRule target = new UnauthenticatedExternalSourceRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        private static StencilRectangle CreateExternal(string name, string? authenticatesItself = null)
        {
            StencilRectangle external = new StencilRectangle
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = ExternalInteractorGenericTypeId,
            };
            external.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = name });
            if (authenticatesItself != null)
            {
                external.Properties.Add(new CustomStringDisplayAttribute { Value = $"AuthenticatesItself:{authenticatesItself}" });
            }

            return external;
        }

        private static StencilEllipse CreateProcess(string name)
        {
            StencilEllipse process = new StencilEllipse
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = ProcessGenericTypeId,
            };
            process.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = name });
            return process;
        }

        private static Connector CreateFlow(Entity source, Entity target)
        {
            return new Connector
            {
                Guid = Guid.NewGuid(),
                SourceGuid = source.Guid,
                TargetGuid = target.Guid,
            };
        }

        private static ThreatModel BuildModel(StencilRectangle external, StencilEllipse process, Connector connector)
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
                            { external.Guid, external },
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
