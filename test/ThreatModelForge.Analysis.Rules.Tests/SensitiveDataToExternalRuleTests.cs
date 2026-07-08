namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the <see cref="SensitiveDataToExternalRule"/> class (TM1030).
    /// </summary>
    [TestClass]
    public class SensitiveDataToExternalRuleTests
    {
        private const string ProcessGenericTypeId = "GE.P";

        private const string ExternalInteractorGenericTypeId = "GE.EI";

        /// <summary>
        /// Verifies the rule's identity and populated metadata.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using SensitiveDataToExternalRule target = new SensitiveDataToExternalRule();
            Assert.AreEqual("TM1030", target.ID);
            Assert.AreEqual(MessageSeverity.Warning, target.Severity);
            Assert.IsNotNull(target.HelpUri);
            Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
            Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
        }

        /// <summary>
        /// A flow carrying a sensitive classification to an external interactor is flagged.
        /// </summary>
        [TestMethod]
        public void FlagsSensitiveFlowToExternal()
        {
            StencilRectangle external = CreateExternal("Partner API");
            Connector edge = EdgeTo(external.Guid, ("DataType", "EUII"));
            MockMessageWriter writer = Evaluate(external, edge);

            Assert.AreEqual(1, writer.Messages.Count);
            Assert.AreSame(edge, writer.Messages[0].Target);
            Assert.IsTrue(writer.Messages[0].Text!.Contains("EUII"));
        }

        /// <summary>
        /// A flow carrying a non-sensitive classification to an external interactor is not flagged.
        /// </summary>
        [TestMethod]
        public void IgnoresNonSensitiveFlowToExternal()
        {
            StencilRectangle external = CreateExternal("Partner API");
            Connector edge = EdgeTo(external.Guid, ("DataType", "Public Non-Personal Data"));
            MockMessageWriter writer = Evaluate(external, edge);

            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// A flow with no data classification to an external interactor is not flagged.
        /// </summary>
        [TestMethod]
        public void IgnoresUnclassifiedFlowToExternal()
        {
            StencilRectangle external = CreateExternal("Partner API");
            Connector edge = EdgeTo(external.Guid);
            MockMessageWriter writer = Evaluate(external, edge);

            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// A sensitive flow whose destination is an internal component is not flagged.
        /// </summary>
        [TestMethod]
        public void IgnoresSensitiveFlowToInternalComponent()
        {
            StencilEllipse process = new StencilEllipse
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = ProcessGenericTypeId,
            };
            process.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = "Billing" });
            Connector edge = EdgeTo(process.Guid, ("DataType", "Customer Content"));

            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Header = "DFD-0" };
            diagram.Borders.Add(process.Guid, process);
            diagram.Lines.Add(edge.Guid, edge);
            ThreatModel model = new ThreatModel { DrawingSurfaceList = { diagram } };

            MockMessageWriter writer = new MockMessageWriter();
            using (SensitiveDataToExternalRule target = new SensitiveDataToExternalRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        private static MockMessageWriter Evaluate(StencilRectangle external, Connector edge)
        {
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Header = "DFD-0" };
            diagram.Borders.Add(external.Guid, external);
            diagram.Lines.Add(edge.Guid, edge);
            ThreatModel model = new ThreatModel { DrawingSurfaceList = { diagram } };

            MockMessageWriter writer = new MockMessageWriter();
            using (SensitiveDataToExternalRule target = new SensitiveDataToExternalRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            return writer;
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

        private static Connector EdgeTo(Guid target, params (string Name, string Value)[] properties)
        {
            Connector edge = new Connector { Guid = Guid.NewGuid(), SourceGuid = Guid.NewGuid(), TargetGuid = target };
            foreach ((string propertyName, string propertyValue) in properties)
            {
                edge.Properties.Add(new CustomStringDisplayAttribute { Value = $"{propertyName}:{propertyValue}" });
            }

            return edge;
        }
    }
}
