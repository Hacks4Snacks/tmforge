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

        /// <summary>Flat rule token expansion is bounded before constructing the result.</summary>
        [TestMethod]
        public void RejectsOversizedExpandedMessage()
        {
            string spec = "{\"rules\":[{\"id\":\"MESSAGE-LIMIT\",\"appliesTo\":\"process\"," +
                "\"message\":\"{name}\",\"when\":{\"property\":\"Marker\"}}]}";
            StencilEllipse process = CreateProcess(new string('x', 65537));
            AddProperties(process, new[] { ("Marker", "set") });

            Assert.Throws<InvalidDataException>(() => Evaluate(spec, ModelWithComponents(process)));
        }

        /// <summary>Malformed and unknown message tokens remain literal without repeated suffix scanning.</summary>
        [TestMethod]
        public void PreservesMalformedAndUnknownMessageTokens()
        {
            string spec = "{\"rules\":[{\"id\":\"TOKENS\",\"appliesTo\":\"process\"," +
                "\"message\":\"{unknown} {name} {{{\",\"when\":{\"property\":\"Marker\"}}]}";
            StencilEllipse process = CreateProcess("Worker");
            AddProperties(process, new[] { ("Marker", "set") });

            MockMessageWriter writer = Evaluate(spec, ModelWithComponents(process));

            Assert.AreEqual("{unknown} " + process.DisplayText() + " {{{", writer.Messages.Single().Text);
        }

        /// <summary>
        /// A v2 property id or alias is canonicalized to the runtime display name before evaluation.
        /// </summary>
        [TestMethod]
        public void VersionTwoPropertyAliasEvaluatesRuntimeDisplayName()
        {
            string spec =
                "{\"schema\":\"tmforge-rules\",\"version\":2," +
                "\"dialect\":\"urn:tmforge:rules:flat-v1\"," +
                "\"pack\":{\"id\":\"alias-pack\",\"name\":\"Alias pack\"}," +
                "\"categories\":[],\"elementTypes\":[]," +
                "\"properties\":[{\"name\":\"Cache Type\",\"aliases\":[\"cacheType\",\"cache-type\"]}]," +
                "\"rules\":[{\"id\":\"CACHE001\",\"appliesTo\":\"process\",\"message\":\"bad cache\"," +
                "\"assert\":{\"property\":\"cache-type\",\"equals\":\"Distributed\"}}]}";

            StencilEllipse distributed = CreateProcess("Worker");
            AddProperties(distributed, new[] { ("Cache Type", "Distributed") });
            Assert.AreEqual(0, Evaluate(spec, ModelWithComponents(distributed)).Messages.Count);

            StencilEllipse local = CreateProcess("Worker");
            AddProperties(local, new[] { ("Cache Type", "Static") });
            Assert.AreEqual(1, Evaluate(spec, ModelWithComponents(local)).Messages.Count);

            IReadOnlyList<Rule> rules = LoadRules(spec);
            Assert.AreEqual("Cache Type", rules.Single().PropertyBindings.Single().PropertyName);
        }

        /// <summary>Lowered flat predicates retain first-value and absent-property behavior.</summary>
        [TestMethod]
        public void LoweredFlatPropertiesPreserveLegacyValueSemantics()
        {
            string firstValueSpec =
                "{\"rules\":[{\"id\":\"FIRST\",\"appliesTo\":\"process\",\"message\":\"x\"," +
                "\"when\":{\"property\":\"Marker\",\"anyOf\":[\"second\"]}}]}";
            StencilEllipse multiValue = CreateProcess("Worker");
            AddProperties(multiValue, new[] { ("Marker", "first"), ("Marker", "second") });
            Assert.AreEqual(0, Evaluate(firstValueSpec, ModelWithComponents(multiValue)).Messages.Count);

            string absentSpec =
                "{\"rules\":[{\"id\":\"ABSENT\",\"appliesTo\":\"process\",\"message\":\"x\"," +
                "\"when\":{\"property\":\"Marker\",\"present\":false," +
                "\"notAnyOf\":[\"blocked\"]}}]}";
            Assert.AreEqual(1, Evaluate(absentSpec, ModelWithComponents(CreateProcess("Worker"))).Messages.Count);
        }

        /// <summary>Lowered flow predicates retain endpoint and negative-boundary semantics.</summary>
        [TestMethod]
        public void LoweredFlatFlowsPreserveRelationalSemantics()
        {
            string spec =
                "{\"rules\":[{\"id\":\"RELATIONAL\",\"appliesTo\":\"flow\",\"message\":\"x\"," +
                "\"when\":{\"crossesTrustBoundary\":false," +
                "\"target\":{\"kind\":\"external\",\"property\":\"Trust\",\"equals\":\"Partner\"}}}]}";
            StencilRectangle external = CreateExternal("Partner");
            AddProperties(external, new[] { ("Trust", "Partner") });
            Connector local = FlowTo(external.Guid, "Push");
            local.SourceX = 10;
            local.SourceY = 11;
            local.TargetX = 12;
            local.TargetY = 15;
            Connector crossing = FlowTo(external.Guid, "Push");
            crossing.SourceX = 0;
            crossing.SourceY = 0;
            crossing.TargetX = 12;
            crossing.TargetY = 15;
            BorderBoundary boundary = CreateBoundary();

            Assert.AreEqual(1, EvaluateEndpointWithBoundary(spec, external, boundary, local).Messages.Count);
            Assert.AreEqual(0, EvaluateEndpointWithBoundary(spec, external, boundary, crossing).Messages.Count);
        }

        private static MockMessageWriter Evaluate(string specJson, ThreatModel model)
        {
            IReadOnlyList<Rule> rules = LoadRules(specJson);
            MockMessageWriter writer = new MockMessageWriter();
            RuleEvaluationContext context = new RuleEvaluationContext(model, writer);
            foreach (Rule rule in rules)
            {
                rule.Evaluate(context);
            }

            return writer;
        }

        private static IReadOnlyList<Rule> LoadRules(string specJson)
        {
            string path = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".tmrules.json");
            File.WriteAllText(path, specJson);
            try
            {
                return DeclarativeRuleProvider.Load(new[] { path });
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

        private static MockMessageWriter EvaluateEndpointWithBoundary(
            string specJson,
            Entity endpoint,
            BorderBoundary boundary,
            Connector flow)
        {
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Header = "DFD-0" };
            diagram.Borders.Add(endpoint.Guid, endpoint);
            diagram.Borders.Add(boundary.Guid, boundary);
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
