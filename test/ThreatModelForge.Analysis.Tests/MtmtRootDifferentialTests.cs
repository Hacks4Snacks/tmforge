namespace ThreatModelForge.Analysis.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Locks the measured difference between the Microsoft Threat Modeling Tool and Threat Model Forge
    /// for the six migrated <c>ROOT</c> threat types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The oracle in <c>root-scope.mtmt.json</c> was captured on Windows from the pinned tool build by
    /// <c>capture-mtmt-root-scope.ps1</c>. It records that the tool declares all six types with the
    /// filter <c>source is 'ROOT'</c> and then generates none of them.
    /// </para>
    /// <para>
    /// That is not a defect in either product. The tool builds its element type chain with
    /// <c>KnowledgeBaseModel.GetElementTypeChain</c>, whose loop stops before appending the virtual
    /// <c>ROOT</c> type, and its <c>IS</c> operator tests that chain, so <c>source is 'ROOT'</c> can
    /// never hold. The six types are inert migration artifacts, which the knowledge base itself
    /// describes as having been migrated from version 3.
    /// </para>
    /// <para>
    /// Threat Model Forge deliberately keeps them live as a per-diagram STRIDE sweep, which is coverage
    /// the tool lost. These tests exist so the difference stays a recorded decision rather than an
    /// accident, and so a change to either side has to restate it.
    /// </para>
    /// </remarks>
    [TestClass]
    public class MtmtRootDifferentialTests
    {
        private const string OraclePath = "Fixtures/MtmtDifferential/root-scope.mtmt.json";

        private static readonly string[] RootTypeIds = { "DU", "EU", "IU", "RU", "SU", "TU" };

        /// <summary>
        /// The pinned tool build declares every ROOT type and generates none of them.
        /// </summary>
        [TestMethod]
        public void ToolDeclaresEveryRootTypeAndGeneratesNone()
        {
            JsonElement oracle = ReadOracle();

            var declared = oracle.GetProperty("rootThreatTypesDeclared").EnumerateArray().ToList();
            CollectionAssert.AreEquivalent(
                RootTypeIds,
                declared.Select(entry => entry.GetProperty("typeId").GetString()).ToArray());
            foreach (JsonElement entry in declared)
            {
                Assert.AreEqual(
                    "source is 'ROOT'",
                    entry.GetProperty("generationFilters").GetString(),
                    "The capture must describe the filter the tool actually evaluated.");
            }

            // Declared but never produced: the types were available to the run and did not match.
            Assert.AreEqual(0, oracle.GetProperty("rootThreatCount").GetInt32());
            Assert.AreEqual("not-generated", oracle.GetProperty("interpretation").GetString());
        }

        /// <summary>
        /// The tool generates once per interaction, so the fixture's asymmetric diagrams do not produce
        /// equal counts. This is what rules out a per-diagram or model-wide reading of the zero above.
        /// </summary>
        [TestMethod]
        public void ToolGeneratesOncePerInteraction()
        {
            JsonElement oracle = ReadOracle();

            var byDiagram = oracle.GetProperty("generatedThreatsByDiagram")
                .EnumerateArray()
                .ToDictionary(
                    entry => entry.GetProperty("diagram").GetString() !,
                    entry => (Lines: entry.GetProperty("lines").GetInt32(),
                        Threats: entry.GetProperty("generatedThreatCount").GetInt32()));

            Assert.AreEqual(1, byDiagram["Diagram A"].Lines);
            Assert.AreEqual(2, byDiagram["Diagram B"].Lines);

            int perInteraction = byDiagram["Diagram A"].Threats;
            Assert.AreEqual(
                perInteraction * 2,
                byDiagram["Diagram B"].Threats,
                "Twice the interactions must yield twice the threats.");
            Assert.AreEqual(
                perInteraction * 3,
                oracle.GetProperty("generatedThreatCount").GetInt32(),
                "The model total must be the sum over its three interactions.");
        }

        /// <summary>
        /// The filter text the tool evaluated compiles to the same source/ROOT predicate Threat Model
        /// Forge evaluates, so the two sides are known to be talking about the same rule.
        /// </summary>
        [TestMethod]
        public void RootFilterCompilesToTheSourceRootPredicate()
        {
            JsonElement oracle = ReadOracle();
            string filter = oracle.GetProperty("rootThreatTypesDeclared")
                .EnumerateArray()
                .First()
                .GetProperty("generationFilters")
                .GetString() !;

            InteractionExpression expression = new MtmtGenerationFilterParser().Parse(filter);

            Assert.AreEqual(InteractionExpression.OperationKind.Type, expression.Operation);
            Assert.AreEqual("source", expression.Subject);
            Assert.AreEqual("ROOT", expression.Type);
            Assert.AreEqual(filter, MtmtGenerationFilterFormatter.Format(expression));
        }

        /// <summary>
        /// Threat Model Forge evaluates each ROOT rule once per diagram, so the six rules yield twelve
        /// findings over the fixture's two diagrams where the tool yields none. The count is
        /// deliberately insensitive to how many interactions a diagram holds.
        /// </summary>
        [TestMethod]
        public void ForgeEvaluatesEveryRootRuleOncePerDiagram()
        {
            DrawingSurfaceModel diagramA = CreateDiagram("Diagram A", interactions: 1);
            DrawingSurfaceModel diagramB = CreateDiagram("Diagram B", interactions: 2);
            ThreatModel model = new ThreatModel { DrawingSurfaceList = { diagramA, diagramB } };
            MockMessageWriter writer = new MockMessageWriter();

            RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(
                new[] { RuleContent.FromJson("mtmt-root.tmrules.json", RootRuleSpec()) },
                null);
            RuleEvaluationContext context = new RuleEvaluationContext(model, writer);
            foreach (Rule rule in bundle.Rules)
            {
                rule.Evaluate(context);
            }

            Assert.AreEqual(
                RootTypeIds.Length * 2,
                writer.Messages.Count,
                "Six ROOT rules over two diagrams must yield twelve findings.");
            foreach (DrawingSurfaceModel diagram in new[] { diagramA, diagramB })
            {
                Assert.AreEqual(
                    RootTypeIds.Length,
                    writer.Messages.Count(message => ReferenceEquals(message.Target, diagram)),
                    "Every ROOT rule must fire exactly once against each diagram.");
            }

            // The recorded divergence: the same six rules produce nothing in the tool.
            Assert.AreEqual(0, ReadOracle().GetProperty("rootThreatCount").GetInt32());
        }

        private static JsonElement ReadOracle()
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(OraclePath));
            return document.RootElement.Clone();
        }

        private static string RootRuleSpec()
        {
            IEnumerable<string> rules = RootTypeIds.Select(id =>
                $"{{\"id\":\"{id}\",\"message\":\"{id} root\"," +
                "\"expression\":{\"subject\":\"source\",\"type\":\"ROOT\"}}");

            return "{\"schema\":\"tmforge-rules\",\"version\":2," +
                "\"dialect\":\"urn:tmforge:rules:interaction-v1\"," +
                "\"pack\":{\"id\":\"mtmt-root\",\"name\":\"MTMT ROOT\"}," +
                "\"rules\":[" + string.Join(",", rules) + "]}";
        }

        private static DrawingSurfaceModel CreateDiagram(string header, int interactions)
        {
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = header };
            StencilRectangle source = CreateEntity<StencilRectangle>("GE.EI", "Client");
            StencilEllipse target = CreateEntity<StencilEllipse>("GE.P", "Service");
            diagram.Borders.Add(source.Guid, source);
            diagram.Borders.Add(target.Guid, target);

            for (int index = 0; index < interactions; index++)
            {
                Connector flow = CreateEntity<Connector>("GE.DF", "Flow " + index);
                flow.SourceGuid = source.Guid;
                flow.TargetGuid = target.Guid;
                diagram.Lines.Add(flow.Guid, flow);
            }

            return diagram;
        }

        private static T CreateEntity<T>(string typeId, string name)
            where T : Entity, new()
        {
            T entity = new T
            {
                Guid = Guid.NewGuid(),
                TypeId = typeId,
                GenericTypeId = typeId,
            };
            entity.Properties.Add(new StringDisplayAttribute { Name = "Name", DisplayName = "Name", Value = name });
            return entity;
        }
    }
}
