namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for declarative (data-defined) rules loaded through <see cref="DeclarativeRuleProvider"/>
    /// and evaluated against a model. These verify that a declarative rule resolves properties,
    /// trust-boundary crossings, and endpoint kinds identically to a compiled rule.
    /// </summary>
    [TestClass]
    public class DeclarativeRuleTests
    {
        private const string StoreGenericTypeId = "GE.DS";

        private const string ExternalGenericTypeId = "GE.EI";

        private const string ProcessGenericTypeId = "GE.P";

        /// <summary>
        /// A datastore requirement flags a store that violates it and passes one that meets it.
        /// </summary>
        [TestMethod]
        public void DataStoreAssertFlagsViolation()
        {
            string spec = "{\"rules\":[{\"id\":\"ACME001\",\"severity\":\"error\",\"appliesTo\":\"datastore\"," +
                "\"message\":\"{name} is not encrypted\",\"assert\":{\"property\":\"Encrypted\",\"notAnyOf\":[\"No\"]}}]}";

            StencilParallelLines unencrypted = CreateStore("DB", ("Encrypted", "No"));
            MockMessageWriter flagged = Evaluate(spec, ModelWithComponents(unencrypted));
            Assert.AreEqual(1, flagged.Messages.Count);
            Assert.AreSame(unencrypted, flagged.Messages[0].Target);
            Assert.IsTrue(flagged.Messages[0].Text!.Contains("DB"));

            StencilParallelLines encrypted = CreateStore("DB", ("Encrypted", "At-rest"));
            MockMessageWriter clean = Evaluate(spec, ModelWithComponents(encrypted));
            Assert.AreEqual(0, clean.Messages.Count);
        }

        /// <summary>
        /// A flow rule combining a property guard, a trust-boundary-crossing guard, and a protocol
        /// requirement flags only a sensitive flow that crosses a boundary over a cleartext protocol.
        /// </summary>
        [TestMethod]
        public void FlowCrossingBoundaryWithSensitiveDataRequiresEncryption()
        {
            string spec = "{\"rules\":[{\"id\":\"ACME002\",\"severity\":\"warning\",\"appliesTo\":\"flow\"," +
                "\"message\":\"{name} sends sensitive data in the clear across a boundary\"," +
                "\"when\":{\"property\":\"DataType\",\"anyOf\":[\"EUII\"],\"crossesTrustBoundary\":true}," +
                "\"assert\":{\"property\":\"Protocol\",\"anyOf\":[\"HTTPS\",\"TLS\",\"mTLS\"]}}]}";

            BorderBoundary border = CreateBoundary();

            Connector cleartextCrossing = CreateFlow("Sync", crossing: true, ("DataType", "EUII"), ("Protocol", "HTTP"));
            Assert.AreEqual(1, EvaluateFlow(spec, border, cleartextCrossing).Messages.Count);

            Connector encryptedCrossing = CreateFlow("Sync", crossing: true, ("DataType", "EUII"), ("Protocol", "HTTPS"));
            Assert.AreEqual(0, EvaluateFlow(spec, border, encryptedCrossing).Messages.Count);

            Connector cleartextInside = CreateFlow("Sync", crossing: false, ("DataType", "EUII"), ("Protocol", "HTTP"));
            Assert.AreEqual(0, EvaluateFlow(spec, border, cleartextInside).Messages.Count);
        }

        /// <summary>
        /// A flow rule guarded on the target endpoint's kind flags a flow to an external interactor and
        /// ignores an otherwise-identical flow to an internal component.
        /// </summary>
        [TestMethod]
        public void FlowToExternalTargetRequiresEncryption()
        {
            string spec = "{\"rules\":[{\"id\":\"ACME003\",\"severity\":\"warning\",\"appliesTo\":\"flow\"," +
                "\"message\":\"{name} reaches an external party over an insecure channel\"," +
                "\"when\":{\"target\":{\"kind\":\"external\"}}," +
                "\"assert\":{\"property\":\"Protocol\",\"anyOf\":[\"HTTPS\"]}}]}";

            StencilRectangle external = CreateExternal("Partner");
            Connector toExternal = FlowTo(external.Guid, "Push", ("Protocol", "HTTP"));
            MockMessageWriter flagged = EvaluateEndpoint(spec, external, toExternal);
            Assert.AreEqual(1, flagged.Messages.Count);
            Assert.AreSame(toExternal, flagged.Messages[0].Target);

            StencilEllipse process = CreateProcess("Worker");
            Connector toProcess = FlowTo(process.Guid, "Push", ("Protocol", "HTTP"));
            MockMessageWriter clean = EvaluateEndpoint(spec, process, toProcess);
            Assert.AreEqual(0, clean.Messages.Count);
        }

        /// <summary>
        /// A rule with a guard and no requirement flags every element that matches the guard.
        /// </summary>
        [TestMethod]
        public void GuardWithoutRequirementFlagsEveryMatch()
        {
            string spec = "{\"rules\":[{\"id\":\"ACME004\",\"severity\":\"info\",\"appliesTo\":\"flow\"," +
                "\"message\":\"{name} crosses a trust boundary\"," +
                "\"when\":{\"crossesTrustBoundary\":true}}]}";

            BorderBoundary border = CreateBoundary();
            Assert.AreEqual(1, EvaluateFlow(spec, border, CreateFlow("Call", crossing: true)).Messages.Count);
            Assert.AreEqual(0, EvaluateFlow(spec, border, CreateFlow("Call", crossing: false)).Messages.Count);
        }

        private static MockMessageWriter Evaluate(string specJson, ThreatModel model)
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".tmrules.json");
            File.WriteAllText(path, specJson);
            try
            {
                IReadOnlyList<Rule> rules = DeclarativeRuleProvider.Load(new[] { path });
                MockMessageWriter writer = new MockMessageWriter();
                RuleEvaluationContext context = new RuleEvaluationContext(model, writer);
                foreach (Rule rule in rules)
                {
                    rule.Evaluate(context);
                }

                return writer;
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static MockMessageWriter EvaluateFlow(string specJson, BorderBoundary border, Connector flow)
        {
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Header = "DFD-0" };
            diagram.Borders.Add(border.Guid, border);
            diagram.Lines.Add(flow.Guid, flow);
            return Evaluate(specJson, new ThreatModel { DrawingSurfaceList = { diagram } });
        }

        private static MockMessageWriter EvaluateEndpoint(string specJson, Entity endpoint, Connector flow)
        {
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Header = "DFD-0" };
            diagram.Borders.Add(endpoint.Guid, endpoint);
            diagram.Lines.Add(flow.Guid, flow);
            return Evaluate(specJson, new ThreatModel { DrawingSurfaceList = { diagram } });
        }

        private static ThreatModel ModelWithComponents(params Entity[] components)
        {
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Header = "DFD-0" };
            foreach (Entity component in components)
            {
                diagram.Borders.Add(component.Guid, component);
            }

            return new ThreatModel { DrawingSurfaceList = { diagram } };
        }

        private static StencilParallelLines CreateStore(string name, params (string Name, string Value)[] properties)
        {
            StencilParallelLines store = new StencilParallelLines { Guid = Guid.NewGuid(), GenericTypeId = StoreGenericTypeId };
            store.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = name });
            AddProperties(store, properties);
            return store;
        }

        private static StencilRectangle CreateExternal(string name)
        {
            StencilRectangle external = new StencilRectangle { Guid = Guid.NewGuid(), GenericTypeId = ExternalGenericTypeId };
            external.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = name });
            return external;
        }

        private static StencilEllipse CreateProcess(string name)
        {
            StencilEllipse process = new StencilEllipse { Guid = Guid.NewGuid(), GenericTypeId = ProcessGenericTypeId };
            process.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = name });
            return process;
        }

        private static BorderBoundary CreateBoundary()
        {
            return new BorderBoundary { Guid = Guid.NewGuid(), Left = 5, Top = 10, Height = 15, Width = 20 };
        }

        private static Connector CreateFlow(string name, bool crossing, params (string Name, string Value)[] properties)
        {
            // The target sits inside the boundary (x in [5,25), y in [10,25)); the source sits inside
            // when the flow stays local and outside when it crosses, so Crosses() is source XOR target.
            Connector flow = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceGuid = Guid.NewGuid(),
                TargetGuid = Guid.NewGuid(),
                SourceX = crossing ? 0 : 10,
                SourceY = crossing ? 0 : 11,
                TargetX = 12,
                TargetY = 15,
            };
            flow.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = name });
            AddProperties(flow, properties);
            return flow;
        }

        private static Connector FlowTo(Guid target, string name, params (string Name, string Value)[] properties)
        {
            Connector flow = new Connector { Guid = Guid.NewGuid(), SourceGuid = Guid.NewGuid(), TargetGuid = target };
            flow.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = name });
            AddProperties(flow, properties);
            return flow;
        }

        private static void AddProperties(Entity entity, (string Name, string Value)[] properties)
        {
            foreach ((string propertyName, string propertyValue) in properties)
            {
                entity.Properties.Add(new CustomStringDisplayAttribute { Value = $"{propertyName}:{propertyValue}" });
            }
        }
    }
}
