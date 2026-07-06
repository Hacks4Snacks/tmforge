namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for the <see cref="OverPrivilegedProcessRule"/> class.
    /// </summary>
    [TestClass]
    public class OverPrivilegedProcessRuleTest
    {
        private const string ProcessGenericTypeId = "GE.P";

        /// <summary>
        /// Unit test for the <see cref="OverPrivilegedProcessRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using (OverPrivilegedProcessRule target = new OverPrivilegedProcessRule())
            {
                Assert.AreEqual("TM1024", target.ID);
                Assert.AreEqual(MessageSeverity.Warning, target.Severity);
                Assert.IsNotNull(target.HelpUri);
                Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
                Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
            }
        }

        /// <summary>
        /// A process running as System is flagged, and the message names the account.
        /// </summary>
        [TestMethod]
        public void EvaluateFlagsSystemProcessTest()
        {
            StencilEllipse process = CreateProcess("Agent", "System");
            ThreatModel model = BuildModel(process);
            MockMessageWriter writer = new MockMessageWriter();

            using (OverPrivilegedProcessRule target = new OverPrivilegedProcessRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(1, writer.Messages.Count);
            Assert.AreSame(process, writer.Messages[0].Target);
            Assert.IsTrue(writer.Messages[0].Text!.Contains("Agent"));
            Assert.IsTrue(writer.Messages[0].Text!.Contains("System"));
        }

        /// <summary>
        /// A process running as Root/Admin is flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateFlagsRootProcessTest()
        {
            StencilEllipse process = CreateProcess("Daemon", "Root/Admin");
            ThreatModel model = BuildModel(process);
            MockMessageWriter writer = new MockMessageWriter();

            using (OverPrivilegedProcessRule target = new OverPrivilegedProcessRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(1, writer.Messages.Count);
        }

        /// <summary>
        /// A process running as a standard user is not flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresStandardUserProcessTest()
        {
            StencilEllipse process = CreateProcess("Web App", "Standard User");
            ThreatModel model = BuildModel(process);
            MockMessageWriter writer = new MockMessageWriter();

            using (OverPrivilegedProcessRule target = new OverPrivilegedProcessRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// A process running under a dedicated service account is not flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresServiceAccountProcessTest()
        {
            StencilEllipse process = CreateProcess("Worker", "Service Account");
            ThreatModel model = BuildModel(process);
            MockMessageWriter writer = new MockMessageWriter();

            using (OverPrivilegedProcessRule target = new OverPrivilegedProcessRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// A process missing the RunningAs property is not flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresProcessWithoutRunningAsTest()
        {
            StencilEllipse process = CreateProcess("Unknown", null);
            ThreatModel model = BuildModel(process);
            MockMessageWriter writer = new MockMessageWriter();

            using (OverPrivilegedProcessRule target = new OverPrivilegedProcessRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        private static StencilEllipse CreateProcess(string name, string? runningAs)
        {
            StencilEllipse process = new StencilEllipse
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = ProcessGenericTypeId,
            };
            process.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = name });
            if (runningAs != null)
            {
                process.Properties.Add(new CustomStringDisplayAttribute { Value = $"RunningAs:{runningAs}" });
            }

            return process;
        }

        private static ThreatModel BuildModel(Entity component)
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
                            { component.Guid, component },
                        },
                    },
                },
            };
        }
    }
}
