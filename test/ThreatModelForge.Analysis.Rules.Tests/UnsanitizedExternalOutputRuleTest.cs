namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the <see cref="UnsanitizedExternalOutputRule"/> class.
    /// </summary>
    [TestClass]
    public class UnsanitizedExternalOutputRuleTest
    {
        private const string ProcessGenericTypeId = "GE.P";
        private const string ExternalInteractorGenericTypeId = "GE.EI";

        /// <summary>
        /// Unit test for the <see cref="UnsanitizedExternalOutputRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using (UnsanitizedExternalOutputRule target = new UnsanitizedExternalOutputRule())
            {
                Assert.AreEqual("TM1018", target.ID);
                Assert.AreEqual(MessageSeverity.Warning, target.Severity);
                Assert.IsNotNull(target.HelpUri);
                Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
                Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
            }
        }

        /// <summary>
        /// A process that sends output to an external entity without sanitizing it is flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateFlagsUnsanitizedExternalOutputTest()
        {
            StencilEllipse process = CreateProcess("Web Server", "No");
            StencilRectangle external = CreateExternal("Browser");
            Connector outbound = CreateOutbound(process, external);
            ThreatModel model = BuildModel(process, external, outbound);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnsanitizedExternalOutputRule target = new UnsanitizedExternalOutputRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(1, writer.Messages.Count);
            Assert.AreSame(process, writer.Messages[0].Target);
            Assert.IsTrue(writer.Messages[0].Text!.Contains("Web Server"));
        }

        /// <summary>
        /// A process that sanitizes its output is not flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresSanitizedProcessTest()
        {
            StencilEllipse process = CreateProcess("Web Server", "Yes");
            StencilRectangle external = CreateExternal("Browser");
            Connector outbound = CreateOutbound(process, external);
            ThreatModel model = BuildModel(process, external, outbound);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnsanitizedExternalOutputRule target = new UnsanitizedExternalOutputRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// An unsanitized process whose output does not reach an external entity is not flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresOutputToNonExternalTest()
        {
            StencilEllipse process = CreateProcess("Web Server", "No");
            StencilEllipse downstream = CreateProcess("Cache", "No");
            Connector outbound = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceGuid = process.Guid,
                TargetGuid = downstream.Guid,
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
                            { downstream.Guid, downstream },
                        },
                        Lines =
                        {
                            { outbound.Guid, outbound },
                        },
                    },
                },
            };
            MockMessageWriter writer = new MockMessageWriter();

            using (UnsanitizedExternalOutputRule target = new UnsanitizedExternalOutputRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        private static StencilEllipse CreateProcess(string name, string sanitizesOutput)
        {
            StencilEllipse process = new StencilEllipse
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = ProcessGenericTypeId,
            };
            process.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = name });
            process.Properties.Add(new CustomStringDisplayAttribute { Value = $"SanitizesOutput:{sanitizesOutput}" });
            return process;
        }

        private static StencilRectangle CreateExternal(string name)
        {
            StencilRectangle external = new StencilRectangle
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = ExternalInteractorGenericTypeId,
            };
            external.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = name });
            return external;
        }

        private static Connector CreateOutbound(StencilEllipse process, StencilRectangle external)
        {
            return new Connector
            {
                Guid = Guid.NewGuid(),
                SourceGuid = process.Guid,
                TargetGuid = external.Guid,
            };
        }

        private static ThreatModel BuildModel(StencilEllipse process, StencilRectangle external, Connector connector)
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
                            { process.Guid, process },
                            { external.Guid, external },
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
