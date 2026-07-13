namespace ThreatModelForge.Analysis.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for the <see cref="DeclarativeRuleProvider"/> class.
    /// </summary>
    [TestClass]
    public class DeclarativeRuleProviderTests
    {
        private const string V2Rule =
            "{\"id\":\"TH112\",\"severity\":\"error\",\"appliesTo\":\"process\"," +
            "\"message\":\"{name} uses an unsafe cache\",\"assert\":{\"property\":\"Cache Type\",\"equals\":\"Distributed\"}," +
            "\"provenance\":{\"sourceId\":\"TH112\",\"categoryId\":\"D\",\"expressions\":[" +
            "{\"role\":\"include\",\"language\":\"urn:tmforge:source:mtmt-generation-filter\",\"text\":\"target is 'GE.P'\"}," +
            "{\"role\":\"exclude\",\"language\":\"urn:tmforge:source:mtmt-generation-filter\",\"text\":\"\"}]}}";

        /// <summary>
        /// Gets or sets the working directory created for each test.
        /// </summary>
        private string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Creates an isolated working directory for the test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            this.WorkingDirectory = Path.Join(Path.GetTempPath(), "tmforge-rules-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.WorkingDirectory);
        }

        /// <summary>
        /// Removes the working directory after the test.
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(this.WorkingDirectory))
            {
                Directory.Delete(this.WorkingDirectory, recursive: true);
            }
        }

        /// <summary>
        /// A valid spec loads a single rule whose id is surfaced verbatim (the string-id constructor)
        /// and whose severity, pack, STRIDE category, and external references are parsed.
        /// </summary>
        [TestMethod]
        public void LoadsValidRuleWithMetadata()
        {
            string spec =
                "{\"rules\":[{" +
                "\"id\":\"ACME001\",\"pack\":\"acme\",\"severity\":\"error\"," +
                "\"appliesTo\":\"datastore\",\"message\":\"{name} is not encrypted\"," +
                "\"stride\":\"InformationDisclosure\",\"threatReferences\":[\"CWE:311\"]," +
                "\"assert\":{\"property\":\"Encrypted\",\"notAnyOf\":[\"No\"]}}]}";
            string path = this.WriteSpec(spec);

            IReadOnlyList<Rule> rules = DeclarativeRuleProvider.Load(new[] { path });

            Assert.AreEqual(1, rules.Count);
            Rule rule = rules[0];
            Assert.AreEqual("ACME001", rule.ID);
            Assert.AreEqual("acme", rule.Pack);
            Assert.AreEqual(MessageSeverity.Error, rule.Severity);
            Assert.AreEqual(StrideCategory.InformationDisclosure, rule.Stride);
            Assert.AreEqual(1, rule.ThreatReferences.Count);
            Assert.AreEqual("CWE-311", rule.ThreatReferences[0].Id);
        }

        /// <summary>Declarative evaluation work is bounded across every rule sharing one context.</summary>
        [TestMethod]
        public void SharesEvaluationBudgetAcrossRules()
        {
            string spec =
                "{\"rules\":[" +
                "{\"id\":\"FIRST\",\"appliesTo\":\"process\",\"message\":\"first\",\"when\":{\"property\":\"Marker\"}}," +
                "{\"id\":\"SECOND\",\"appliesTo\":\"process\",\"message\":\"second\",\"when\":{\"property\":\"Marker\"}}]}";
            IReadOnlyList<Rule> rules = DeclarativeRuleProvider.Load(new[] { this.WriteSpec(spec) });
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "DFD-0" };
            StencilEllipse process = CreateEntity<StencilEllipse>("GE.P", "GE.P", "Worker");
            process.Properties.Add(new CustomStringDisplayAttribute { Value = "Marker:set" });
            diagram.Borders.Add(process.Guid, process);
            RuleEvaluationContext context = new RuleEvaluationContext(
                new ThreatModel { DrawingSurfaceList = { diagram } },
                new MockMessageWriter());

            rules[0].Evaluate(context);
            context.SetDeclarativeOperationLimit(context.GetDeclarativeOperationCount());

            Assert.Throws<InvalidDataException>(() => rules[1].Evaluate(context));
        }

        /// <summary>
        /// A version 2 envelope owns the pack identity and namespaces each source rule id with it.
        /// </summary>
        [TestMethod]
        public void LoadsVersionTwoEnvelopeWithEffectiveIdentity()
        {
            string spec = VersionTwoSpec("azure-template-a1b2c3d4", V2Rule);
            string path = this.WriteSpec(spec);

            RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(new[] { path });

            Assert.AreEqual(1, bundle.Rules.Count);
            Rule rule = bundle.Rules[0];
            Assert.AreEqual("azure-template-a1b2c3d4/TH112", rule.ID);
            Assert.AreEqual("azure-template-a1b2c3d4", rule.Pack);
            Assert.AreEqual("TH112", rule.Provenance!.SourceId);
            Assert.AreEqual("target is 'GE.P'", rule.Provenance.Expressions.Single(expression => expression.Role == "include").Text);

            Assert.AreEqual(1, bundle.Packs.Count);
            RulePackDefinition pack = bundle.Packs[0];
            Assert.AreEqual("Azure Template", pack.Name);
            Assert.IsTrue(pack.Fingerprint!.StartsWith("sha256:", StringComparison.Ordinal));
            Assert.AreEqual("D", pack.Categories.Single().Id);
            Assert.AreEqual("GE.P", pack.ElementTypes.Single().Id);
            Assert.AreEqual("Cache Type", pack.Properties.Single().Name);
            Assert.AreSame(pack, rule.PackDefinition);
        }

        /// <summary>
        /// Version 2 metadata is importer-neutral: a non-MTMT source uses generic source and
        /// provenance fields without manifests or GenerationFilters terminology.
        /// </summary>
        [TestMethod]
        public void LoadsImporterNeutralEnvelopeMetadata()
        {
            string spec =
                "{\"schema\":\"tmforge-rules\",\"version\":2," +
                "\"dialect\":\"urn:tmforge:rules:flat-v1\"," +
                "\"pack\":{\"id\":\"otm-pack\",\"name\":\"OTM pack\",\"source\":{" +
                "\"type\":\"urn:tmforge:source:otm\",\"name\":\"model.otm\",\"id\":\"catalog-42\"," +
                "\"version\":\"1.0\",\"uri\":\"https://example.test/model.otm\",\"fingerprint\":\"sha256:source\"}}," +
                "\"categories\":[{\"id\":\"security\",\"name\":\"Security\"}],\"elementTypes\":[]," +
                "\"properties\":[{\"name\":\"security/encrypted\",\"aliases\":[\"encryption-state\"]," +
                "\"allowedValues\":[\"Yes\",\"No\"]}]," +
                "\"rules\":[{\"id\":\"OTM-1\",\"appliesTo\":\"datastore\",\"message\":\"x\"," +
                "\"assert\":{\"property\":\"encryption-state\",\"equals\":\"Yes\"}," +
                "\"provenance\":{\"sourceId\":\"rule-1\",\"categoryId\":\"security\",\"location\":\"/threats/0\"," +
                "\"expressions\":[{\"role\":\"condition\",\"language\":\"urn:example:otm-expression\",\"text\":\"encrypted = true\"}]}}]}";
            string path = this.WriteSpec(spec);

            RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(new[] { path });

            Assert.AreEqual(1, bundle.Rules.Count);
            Rule rule = bundle.Rules.Single();
            Assert.AreEqual("otm-pack/OTM-1", rule.ID);
            Assert.AreEqual("urn:tmforge:rules:flat-v1", rule.PackDefinition!.Dialect);
            Assert.AreEqual("urn:tmforge:source:otm", rule.PackDefinition.Source!.Type);
            Assert.AreEqual("catalog-42", rule.PackDefinition.Source.Id);
            Assert.AreEqual("rule-1", rule.Provenance!.SourceId);
            Assert.AreEqual("urn:example:otm-expression", rule.Provenance.Expressions.Single().Language);
            Assert.AreEqual("security/encrypted", rule.PropertyBindings.Single().PropertyName);
        }

        /// <summary>
        /// The first new dialect accepts the recursive interaction AST independently of source type.
        /// </summary>
        [TestMethod]
        public void LoadsInteractionVersionOneDialect()
        {
            string spec =
                "{\"schema\":\"tmforge-rules\",\"version\":2," +
                "\"dialect\":\"urn:tmforge:rules:interaction-v1\"," +
                "\"pack\":{\"id\":\"interaction-pack\",\"name\":\"Interaction pack\"}," +
                "\"categories\":[],\"elementTypes\":[{\"id\":\"GE.P\",\"name\":\"Process\",\"parentId\":\"ROOT\"}]," +
                "\"properties\":[{\"name\":\"Protocol\"}],\"rules\":[{\"id\":\"I1\",\"message\":\"{source.Name} to {target.Name} via {flow.Name}\"," +
                "\"expression\":{\"allOf\":[{\"subject\":\"source\",\"type\":\"GE.P\"},{\"not\":{" +
                "\"subject\":\"flow\",\"property\":\"Protocol\",\"valueIn\":[\"TLS\"]}}]}}]}";
            string path = this.WriteSpec(spec);

            RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(new[] { path });

            Assert.AreEqual(1, bundle.Rules.Count);
            Assert.AreEqual("interaction-pack/I1", bundle.Rules.Single().ID);
            Assert.AreEqual("urn:tmforge:rules:interaction-v1", bundle.Rules.Single().PackDefinition!.Dialect);
        }

        /// <summary>
        /// Interaction rules evaluate hierarchy-aware type, property, and boundary predicates over flows,
        /// expand message tokens case-insensitively, and evaluate ROOT once per diagram.
        /// </summary>
        [TestMethod]
        public void EvaluatesInteractionVersionOneDialect()
        {
            string spec =
                "{\"schema\":\"tmforge-rules\",\"version\":2," +
                "\"dialect\":\"urn:tmforge:rules:interaction-v1\"," +
                "\"pack\":{\"id\":\"interaction-pack\",\"name\":\"Interaction pack\"}," +
                "\"elementTypes\":[" +
                "{\"id\":\"GE.P\",\"name\":\"Process\",\"parentId\":\"ROOT\"}," +
                "{\"id\":\"SE.P.Web\",\"name\":\"Web process\",\"parentId\":\"GE.P\"}," +
                "{\"id\":\"GE.TB.B\",\"name\":\"Boundary\",\"parentId\":\"ROOT\"}," +
                "{\"id\":\"SE.TB.Azure\",\"name\":\"Azure boundary\",\"parentId\":\"GE.TB.B\"}]," +
                "\"properties\":[{\"name\":\"Protocol\",\"aliases\":[\"wireProtocol\"],\"allowedValues\":[\"HTTP\",\"TLS\"]}]," +
                "\"rules\":[" +
                "{\"id\":\"FLOW\",\"message\":\"{SOURCE.name} to {target.Name} via {Flow.Name}; {unknown}\"," +
                "\"expression\":{\"allOf\":[" +
                "{\"subject\":\"source\",\"type\":\"GE.P\"}," +
                "{\"subject\":\"flow\",\"property\":\"wireProtocol\",\"valueIn\":[\"HTTP\"]}," +
                "{\"crosses\":\"GE.TB.B\"}]}} ," +
                "{\"id\":\"ROOT\",\"message\":\"Diagram root\"," +
                "\"expression\":{\"subject\":\"source\",\"type\":\"ROOT\"}}]}";
            RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(new[] { this.WriteSpec(spec) });

            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "DFD-0" };
            StencilEllipse source = CreateEntity<StencilEllipse>("SE.P.Web", "GE.P", "{target.Name}");
            StencilRectangle target = CreateEntity<StencilRectangle>("GE.EI", "GE.EI", "Browser");
            BorderBoundary boundary = CreateEntity<BorderBoundary>("SE.TB.Azure", "GE.TB.B", "Azure");
            boundary.Left = 5;
            boundary.Top = 5;
            boundary.Width = 20;
            boundary.Height = 20;
            diagram.Borders.Add(source.Guid, source);
            diagram.Borders.Add(target.Guid, target);
            diagram.Borders.Add(boundary.Guid, boundary);

            Connector flow = CreateEntity<Connector>("GE.DF", "GE.DF", "Request");
            flow.SourceGuid = source.Guid;
            flow.TargetGuid = target.Guid;
            flow.SourceX = 0;
            flow.SourceY = 0;
            flow.TargetX = 10;
            flow.TargetY = 10;
            flow.Properties.Add(new CustomStringDisplayAttribute { Value = "Protocol:HTTP" });
            diagram.Lines.Add(flow.Guid, flow);

            ThreatModel model = new ThreatModel { DrawingSurfaceList = { diagram } };
            MockMessageWriter writer = new MockMessageWriter();
            RuleEvaluationContext context = new RuleEvaluationContext(model, writer);
            foreach (Rule rule in bundle.Rules)
            {
                rule.Evaluate(context);
            }

            Assert.AreEqual(2, writer.Messages.Count);
            Message flowMessage = writer.Messages.Single(message => message.Source!.ID.EndsWith("/FLOW", StringComparison.Ordinal));
            Assert.AreEqual("{target.Name} to Browser via Request; {unknown}", flowMessage.Text);
            Assert.AreSame(flow, flowMessage.Target);
            Rule flowRule = bundle.Rules.Single(rule => rule.ID.EndsWith("/FLOW", StringComparison.Ordinal));
            Assert.AreEqual("Protocol", flowRule.PropertyBindings.Single().PropertyName);
            Message rootMessage = writer.Messages.Single(message => message.Source!.ID.EndsWith("/ROOT", StringComparison.Ordinal));
            Assert.AreEqual("Diagram root", rootMessage.Text);
            Assert.AreSame(diagram, rootMessage.Target);
        }

        /// <summary>
        /// Crosses evaluates false on synthetic ROOT contexts without dereferencing a missing flow.
        /// </summary>
        [TestMethod]
        public void RootCompositeCrossesExpressionsDoNotCrash()
        {
            string root = "{\"subject\":\"source\",\"type\":\"ROOT\"}";
            string crosses = "{\"crosses\":\"GE.TB.B\"}";
            string spec =
                "{\"schema\":\"tmforge-rules\",\"version\":2," +
                "\"dialect\":\"urn:tmforge:rules:interaction-v1\"," +
                "\"pack\":{\"id\":\"root-composites\",\"name\":\"ROOT composites\"}," +
                "\"elementTypes\":[{\"id\":\"GE.TB.B\",\"name\":\"Boundary\",\"parentId\":\"ROOT\"}]," +
                "\"rules\":[" +
                $"{{\"id\":\"ALL\",\"message\":\"all\",\"expression\":{{\"allOf\":[{root},{crosses}]}}}}," +
                $"{{\"id\":\"ANY-CROSS-FIRST\",\"message\":\"any1\",\"expression\":{{\"anyOf\":[{crosses},{root}]}}}}," +
                $"{{\"id\":\"ANY-ROOT-FIRST\",\"message\":\"any2\",\"expression\":{{\"anyOf\":[{root},{crosses}]}}}}," +
                "{\"id\":\"NOT-CROSSES\",\"message\":\"not\",\"expression\":{\"allOf\":[" +
                root + ",{\"not\":" + crosses + "}]}}]}";
            RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(new[] { this.WriteSpec(spec) });
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "DFD-0" };
            ThreatModel model = new ThreatModel { DrawingSurfaceList = { diagram } };
            MockMessageWriter writer = new MockMessageWriter();
            RuleEvaluationContext context = new RuleEvaluationContext(model, writer);

            foreach (Rule rule in bundle.Rules)
            {
                rule.Evaluate(context);
            }

            CollectionAssert.AreEquivalent(
                new[] { "any1", "any2", "not" },
                writer.Messages.Select(message => message.Text).ToArray());
        }

        /// <summary>Interaction token expansion is bounded before constructing the result.</summary>
        [TestMethod]
        public void RejectsOversizedInteractionMessage()
        {
            string spec =
                "{\"schema\":\"tmforge-rules\",\"version\":2," +
                "\"dialect\":\"urn:tmforge:rules:interaction-v1\"," +
                "\"pack\":{\"id\":\"message-limit\",\"name\":\"Message limit\"}," +
                "\"elementTypes\":[{\"id\":\"GE.P\",\"name\":\"Process\",\"parentId\":\"ROOT\"}]," +
                "\"rules\":[{\"id\":\"LIMIT\",\"message\":\"{source.Name}\"," +
                "\"expression\":{\"subject\":\"source\",\"type\":\"GE.P\"}}]}";
            Rule rule = DeclarativeRuleProvider.LoadBundle(new[] { this.WriteSpec(spec) }).Rules.Single();
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "DFD-0" };
            StencilEllipse source = CreateEntity<StencilEllipse>("GE.P", "GE.P", new string('x', 65537));
            StencilRectangle target = CreateEntity<StencilRectangle>("GE.EI", "GE.EI", "Target");
            diagram.Borders.Add(source.Guid, source);
            diagram.Borders.Add(target.Guid, target);
            Connector flow = CreateEntity<Connector>("GE.DF", "GE.DF", "Flow");
            flow.SourceGuid = source.Guid;
            flow.TargetGuid = target.Guid;
            diagram.Lines.Add(flow.Guid, flow);
            RuleEvaluationContext context = new RuleEvaluationContext(
                new ThreatModel { DrawingSurfaceList = { diagram } },
                new MockMessageWriter());

            Assert.Throws<InvalidDataException>(() => rule.Evaluate(context));
        }

        /// <summary>
        /// Unknown dialects and unresolved interaction catalog references are rejected even when a
        /// document has no rules or otherwise has a valid recursive expression shape.
        /// </summary>
        [TestMethod]
        public void RejectsUnknownDialectAndInteractionReferences()
        {
            string unknownDialect =
                "{\"schema\":\"tmforge-rules\",\"version\":2," +
                "\"dialect\":\"urn:example:unknown\"," +
                "\"pack\":{\"id\":\"unknown\",\"name\":\"Unknown\"},\"rules\":[]}";
            string unknownPath = this.WriteSpec(unknownDialect, "unknown-dialect.tmrules.json");
            List<string> unknownDiagnostics = new List<string>();

            RuleBundle unknown = DeclarativeRuleProvider.LoadBundle(new[] { unknownPath }, unknownDiagnostics.Add);

            Assert.AreEqual(0, unknown.Rules.Count);
            Assert.AreEqual(0, unknown.Packs.Count);
            Assert.IsTrue(unknownDiagnostics.Any(message => message.Contains("unknown rule dialect")));

            string interaction =
                "{\"schema\":\"tmforge-rules\",\"version\":2," +
                "\"dialect\":\"urn:tmforge:rules:interaction-v1\"," +
                "\"pack\":{\"id\":\"bad-reference\",\"name\":\"Bad reference\"}," +
                "\"elementTypes\":[{\"id\":\"GE.P\",\"name\":\"Process\",\"parentId\":\"ROOT\"}]," +
                "\"properties\":[{\"name\":\"Protocol\"}]," +
                "\"rules\":[{\"id\":\"BAD\",\"message\":\"x\"," +
                "\"expression\":{\"allOf\":[" +
                "{\"subject\":\"source\",\"type\":\"GE.UNKNOWN\"}," +
                "{\"subject\":\"flow\",\"property\":\"Missing\",\"valueIn\":[\"x\"]}]}}]}";
            string interactionPath = this.WriteSpec(interaction, "bad-reference.tmrules.json");
            List<string> interactionDiagnostics = new List<string>();

            RuleBundle invalid = DeclarativeRuleProvider.LoadBundle(new[] { interactionPath }, interactionDiagnostics.Add);

            Assert.AreEqual(0, invalid.Rules.Count);
            Assert.AreEqual(0, invalid.Packs.Count);
            Assert.IsTrue(interactionDiagnostics.Any(message => message.Contains("unknown element type 'GE.UNKNOWN'")));
        }

        /// <summary>Catalog-backed provenance and interaction values must resolve exactly.</summary>
        [TestMethod]
        public void RejectsUnknownCategoryAndPropertyValueReferences()
        {
            string unknownCategory = VersionTwoSpec("bad-category", V2Rule)
                .Replace("\"categoryId\":\"D\"", "\"categoryId\":\"missing\"");
            List<string> diagnostics = new List<string>();

            RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(
                new[] { this.WriteSpec(unknownCategory, "bad-category.tmrules.json") },
                diagnostics.Add);

            Assert.AreEqual(0, bundle.Rules.Count);
            Assert.IsTrue(diagnostics.Any(message => message.Contains("unknown category 'missing'")));

            string flatUnknownValue = VersionTwoSpec("bad-flat-value", V2Rule)
                .Replace("\"equals\":\"Distributed\"", "\"equals\":\"Distibuted\"");
            diagnostics.Clear();

            bundle = DeclarativeRuleProvider.LoadBundle(
                new[] { this.WriteSpec(flatUnknownValue, "bad-flat-value.tmrules.json") },
                diagnostics.Add);

            Assert.AreEqual(0, bundle.Rules.Count);
            Assert.IsTrue(diagnostics.Any(message => message.Contains("unknown value 'Distibuted'")));

            string unknownValue =
                "{\"schema\":\"tmforge-rules\",\"version\":2," +
                "\"dialect\":\"urn:tmforge:rules:interaction-v1\"," +
                "\"pack\":{\"id\":\"bad-value\",\"name\":\"Bad value\"}," +
                "\"properties\":[{\"name\":\"Protocol\",\"allowedValues\":[\"HTTP\",\"TLS\"]}]," +
                "\"rules\":[{\"id\":\"BAD-VALUE\",\"message\":\"x\"," +
                "\"expression\":{\"subject\":\"flow\",\"property\":\"Protocol\",\"valueIn\":[\"TLX\"]}}]}";
            diagnostics.Clear();

            bundle = DeclarativeRuleProvider.LoadBundle(
                new[] { this.WriteSpec(unknownValue, "bad-value.tmrules.json") },
                diagnostics.Add);

            Assert.AreEqual(0, bundle.Rules.Count);
            Assert.IsTrue(diagnostics.Any(message => message.Contains("unknown value 'TLX'")));
        }

        /// <summary>ROOT is reserved for the synthetic source predicate and cannot be declared.</summary>
        [TestMethod]
        public void RejectsInvalidRootUsage()
        {
            string targetRoot =
                "{\"schema\":\"tmforge-rules\",\"version\":2," +
                "\"dialect\":\"urn:tmforge:rules:interaction-v1\"," +
                "\"pack\":{\"id\":\"target-root\",\"name\":\"Target ROOT\"}," +
                "\"rules\":[{\"id\":\"BAD-ROOT\",\"message\":\"x\"," +
                "\"expression\":{\"subject\":\"target\",\"type\":\"ROOT\"}}]}";
            List<string> diagnostics = new List<string>();

            RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(
                new[] { this.WriteSpec(targetRoot, "target-root.tmrules.json") },
                diagnostics.Add);

            Assert.AreEqual(0, bundle.Rules.Count);
            Assert.IsTrue(diagnostics.Any(message => message.Contains("only valid for source")));

            string declaredRoot = targetRoot
                .Replace("target-root", "declared-root")
                .Replace("Target ROOT", "Declared ROOT")
                .Replace(
                    "\"rules\":[",
                    "\"elementTypes\":[{\"id\":\"ROOT\",\"name\":\"Root\"}],\"rules\":[")
                .Replace("\"subject\":\"target\"", "\"subject\":\"source\"");
            diagnostics.Clear();

            bundle = DeclarativeRuleProvider.LoadBundle(
                new[] { this.WriteSpec(declaredRoot, "declared-root.tmrules.json") },
                diagnostics.Add);

            Assert.AreEqual(0, bundle.Rules.Count);
            Assert.IsTrue(diagnostics.Any(message => message.Contains("reserved")));
        }

        /// <summary>
        /// Source ids that collide across different packs remain distinct after namespacing.
        /// </summary>
        [TestMethod]
        public void SameSourceRuleIdInDifferentPacksLoadsDistinctRules()
        {
            string first = this.WriteSpec(VersionTwoSpec("pack-a", V2Rule));
            string second = this.WriteSpec(VersionTwoSpec("pack-b", V2Rule));

            RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(new[] { first, second });

            CollectionAssert.AreEquivalent(
                new[] { "pack-a/TH112", "pack-b/TH112" },
                bundle.Rules.Select(rule => rule.ID).ToArray());
        }

        /// <summary>Repeated and overlapping source paths load each physical file once.</summary>
        [TestMethod]
        public void RepeatedInputPathsLoadOnce()
        {
            string path = this.WriteSpec(VersionTwoSpec("once-pack", V2Rule), "once.tmrules.json");

            RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(new[] { path, this.WorkingDirectory, path });

            Assert.AreEqual(1, bundle.Packs.Count);
            Assert.AreEqual(1, bundle.Rules.Count);
            Assert.AreEqual("once-pack/TH112", bundle.Rules.Single().ID);
        }

        /// <summary>
        /// Duplicate effective ids reject every declaration, independent of declaration order.
        /// </summary>
        [TestMethod]
        public void DuplicateEffectiveRuleIdsAreOrderIndependentErrors()
        {
            string duplicate = V2Rule.Replace("\"TH112\"", "\"th112\"");
            List<string> firstDiagnostics = new List<string>();
            string firstPath = this.WriteSpec(
                VersionTwoSpec("duplicate-pack", V2Rule + "," + duplicate),
                "duplicate.tmrules.json");
            RuleBundle first = DeclarativeRuleProvider.LoadBundle(new[] { firstPath }, firstDiagnostics.Add);

            List<string> secondDiagnostics = new List<string>();
            string secondPath = this.WriteSpec(
                VersionTwoSpec("duplicate-pack", duplicate + "," + V2Rule),
                "duplicate.tmrules.json");
            RuleBundle second = DeclarativeRuleProvider.LoadBundle(new[] { secondPath }, secondDiagnostics.Add);

            Assert.AreEqual(0, first.Rules.Count);
            Assert.AreEqual(0, second.Rules.Count);
            Assert.AreEqual(1, firstDiagnostics.Count);
            Assert.AreEqual(1, secondDiagnostics.Count);
            Assert.AreEqual(firstDiagnostics[0], secondDiagnostics[0]);
            StringAssert.Contains(firstDiagnostics[0], "duplicate effective rule id 'duplicate-pack/TH112'");
        }

        /// <summary>
        /// Unsupported version markers are rejected rather than being interpreted as legacy files.
        /// </summary>
        [TestMethod]
        public void RejectsUnsupportedVersion()
        {
            string path = this.WriteSpec(VersionTwoSpec("versioned-pack", V2Rule).Replace("\"version\":2", "\"version\":3"));
            List<string> diagnostics = new List<string>();

            RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(new[] { path }, diagnostics.Add);

            Assert.AreEqual(0, bundle.Rules.Count);
            Assert.IsTrue(diagnostics.Any(message => message.Contains("version 2")));
        }

        /// <summary>
        /// Version 2 rejects unknown members so a misspelled catalog or rule field cannot be ignored.
        /// </summary>
        [TestMethod]
        public void RejectsUnknownVersionTwoMembers()
        {
            string path = this.WriteSpec(VersionTwoSpec("strict-pack", V2Rule).Replace("\"allowedValues\"", "\"allowedValuse\""));
            List<string> diagnostics = new List<string>();

            RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(new[] { path }, diagnostics.Add);

            Assert.AreEqual(0, bundle.Rules.Count);
            Assert.IsTrue(diagnostics.Any(message => message.Contains("allowedValuse")));
        }

        /// <summary>
        /// Numeric enum text is not a valid severity or STRIDE name even when it maps to an enum value.
        /// </summary>
        [TestMethod]
        public void RejectsNumericEnumText()
        {
            string numericSeverity = V2Rule.Replace("\"severity\":\"error\"", "\"severity\":\"3\"");
            string severityPath = this.WriteSpec(VersionTwoSpec("numeric-severity", numericSeverity));
            List<string> severityDiagnostics = new List<string>();

            RuleBundle severityBundle = DeclarativeRuleProvider.LoadBundle(new[] { severityPath }, severityDiagnostics.Add);

            Assert.AreEqual(0, severityBundle.Rules.Count);
            Assert.IsTrue(severityDiagnostics.Any(message => message.Contains("unknown severity '3'")));

            string numericStride = V2Rule.Replace("\"severity\":\"error\"", "\"severity\":\"error\",\"stride\":\"6\"");
            string stridePath = this.WriteSpec(VersionTwoSpec("numeric-stride", numericStride));
            List<string> strideDiagnostics = new List<string>();

            RuleBundle strideBundle = DeclarativeRuleProvider.LoadBundle(new[] { stridePath }, strideDiagnostics.Add);

            Assert.AreEqual(0, strideBundle.Rules.Count);
            Assert.IsTrue(strideDiagnostics.Any(message => message.Contains("unknown stride '6'")));
        }

        /// <summary>
        /// Legacy files keep case-insensitive enum names but reject numeric enum syntax.
        /// </summary>
        [TestMethod]
        public void LegacyRulesRejectNumericEnumText()
        {
            string severityPath = this.WriteSpec(
                "{\"rules\":[{\"id\":\"LEGACY-SEV\",\"severity\":\"3\",\"appliesTo\":\"process\"," +
                "\"message\":\"x\",\"when\":{\"property\":\"P\"}}]}");
            List<string> severityDiagnostics = new List<string>();

            IReadOnlyList<Rule> severityRules = DeclarativeRuleProvider.Load(new[] { severityPath }, severityDiagnostics.Add);

            Assert.AreEqual(0, severityRules.Count);
            Assert.IsTrue(severityDiagnostics.Any(message => message.Contains("unknown severity '3'")));

            string stridePath = this.WriteSpec(
                "{\"rules\":[{\"id\":\"LEGACY-STRIDE\",\"stride\":\"6\",\"appliesTo\":\"process\"," +
                "\"message\":\"x\",\"when\":{\"property\":\"P\"}}]}");
            List<string> strideDiagnostics = new List<string>();

            IReadOnlyList<Rule> strideRules = DeclarativeRuleProvider.Load(new[] { stridePath }, strideDiagnostics.Add);

            Assert.AreEqual(0, strideRules.Count);
            Assert.IsTrue(strideDiagnostics.Any(message => message.Contains("unknown stride '6'")));
        }

        /// <summary>
        /// Runtime validation enforces the strict rule shape published by the v2 JSON Schema.
        /// </summary>
        [TestMethod]
        public void RejectsSchemaInvalidVersionTwoRuleShapes()
        {
            string[] invalidRules =
            {
                V2Rule.Replace("\"severity\":\"error\"", "\"severity\":\"ERROR\""),
                V2Rule.Replace("\"appliesTo\":\"process\"", "\"appliesTo\":\"Process\""),
                V2Rule.Replace("\"severity\":\"error\"", "\"severity\":\"error\",\"stride\":\"informationdisclosure\""),
                V2Rule.Replace("\"id\":\"TH112\"", "\"id\":\"bad/id\""),
                V2Rule.Replace("\"severity\":\"error\"", "\"pack\":\"override\",\"severity\":\"error\""),
                V2Rule.Replace("\"assert\":{\"property\":\"Cache Type\",\"equals\":\"Distributed\"},", string.Empty),
                V2Rule.Replace(
                    "\"assert\":{\"property\":\"Cache Type\",\"equals\":\"Distributed\"}",
                    "\"when\":{\"source\":{\"kind\":\"Widget\"}}"),
                V2Rule.Replace(
                    "\"assert\":{\"property\":\"Cache Type\",\"equals\":\"Distributed\"}",
                    "\"assert\":{\"equals\":\"Distributed\"}"),
                V2Rule.Replace(
                    "\"assert\":{\"property\":\"Cache Type\",\"equals\":\"Distributed\"}",
                    "\"assert\":{\"property\":\"Cache Type\",\"anyOf\":[]}"),
            };

            for (int index = 0; index < invalidRules.Length; index++)
            {
                string path = this.WriteSpec(
                    VersionTwoSpec($"invalid-{index}", invalidRules[index]),
                    $"invalid-{index}.tmrules.json");
                List<string> diagnostics = new List<string>();

                RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(new[] { path }, diagnostics.Add);

                Assert.AreEqual(0, bundle.Rules.Count, $"Invalid case {index} unexpectedly loaded.");
                Assert.IsTrue(diagnostics.Count > 0, $"Invalid case {index} produced no diagnostic.");
            }
        }

        /// <summary>
        /// Generic source metadata requires a namespaced type and an absolute URI when present.
        /// </summary>
        [TestMethod]
        public void RejectsInvalidSourceMetadata()
        {
            string invalidType = VersionTwoSpec("bad-source", V2Rule, sourceType: "custom")
                .Replace("urn:tmforge:source:custom", "plugin");
            string typePath = this.WriteSpec(invalidType);
            List<string> typeDiagnostics = new List<string>();

            RuleBundle typeBundle = DeclarativeRuleProvider.LoadBundle(new[] { typePath }, typeDiagnostics.Add);

            Assert.AreEqual(0, typeBundle.Rules.Count);
            Assert.IsTrue(typeDiagnostics.Any(message => message.Contains("namespaced identifier")));

            string invalidUri = VersionTwoSpec("bad-uri", V2Rule, sourceType: "custom")
                .Replace("\"type\":\"urn:tmforge:source:custom\"", "\"type\":\"urn:tmforge:source:custom\",\"uri\":\"relative/path\"");
            string uriPath = this.WriteSpec(invalidUri);
            List<string> uriDiagnostics = new List<string>();

            RuleBundle uriBundle = DeclarativeRuleProvider.LoadBundle(new[] { uriPath }, uriDiagnostics.Add);

            Assert.AreEqual(0, uriBundle.Rules.Count);
            Assert.IsTrue(uriDiagnostics.Any(message => message.Contains("uri must be absolute")));
        }

        /// <summary>
        /// Explicit version/catalog markers cannot be null to downgrade a document into legacy mode.
        /// </summary>
        [TestMethod]
        public void RejectsNullVersionMarkersAndCatalogs()
        {
            string markers = this.WriteSpec(
                "{\"schema\":null,\"version\":null,\"pack\":null,\"rules\":[]}",
                "null-markers.tmrules.json");
            List<string> markerDiagnostics = new List<string>();

            RuleBundle markerBundle = DeclarativeRuleProvider.LoadBundle(new[] { markers }, markerDiagnostics.Add);

            Assert.AreEqual(0, markerBundle.Rules.Count);
            Assert.IsTrue(markerDiagnostics.Any(message => message.Contains("schema 'tmforge-rules' version 2")));

            string nullCatalog = VersionTwoSpec("null-catalog", V2Rule).Replace(
                "\"categories\":[{\"id\":\"D\",\"name\":\"Denial of Service\"}]",
                "\"categories\":null");
            string catalogPath = this.WriteSpec(nullCatalog, "null-catalog.tmrules.json");
            List<string> catalogDiagnostics = new List<string>();

            RuleBundle catalogBundle = DeclarativeRuleProvider.LoadBundle(new[] { catalogPath }, catalogDiagnostics.Add);

            Assert.AreEqual(0, catalogBundle.Rules.Count);
            Assert.IsTrue(catalogDiagnostics.Count > 0);
        }

        /// <summary>Version 2 rejects explicit nulls for optional nested members just as its schema does.</summary>
        [TestMethod]
        public void RejectsExplicitNullVersionTwoMembers()
        {
            string spec = VersionTwoSpec("nested-null", V2Rule)
                .Replace(
                    "\"name\":\"Azure Template\"",
                    "\"name\":\"Azure Template\",\"description\":null");
            List<string> diagnostics = new List<string>();

            RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(
                new[] { this.WriteSpec(spec, "nested-null.tmrules.json") },
                diagnostics.Add);

            Assert.AreEqual(0, bundle.Rules.Count);
            Assert.IsTrue(diagnostics.Any(message => message.Contains("explicit null")));
        }

        /// <summary>Semantic interaction depth 64 loads; depth 65 reaches the explicit semantic limit.</summary>
        [TestMethod]
        public void EnforcesSemanticInteractionExpressionDepth()
        {
            RuleBundle acceptedNot = DeclarativeRuleProvider.LoadBundle(
                new[] { this.WriteSpec(InteractionSpecWithDepth(64, useArray: false), "not-depth-64.tmrules.json") });
            RuleBundle acceptedAll = DeclarativeRuleProvider.LoadBundle(
                new[] { this.WriteSpec(InteractionSpecWithDepth(64, useArray: true), "all-depth-64.tmrules.json") });
            List<string> notDiagnostics = new List<string>();
            List<string> allDiagnostics = new List<string>();

            RuleBundle rejectedNot = DeclarativeRuleProvider.LoadBundle(
                new[] { this.WriteSpec(InteractionSpecWithDepth(65, useArray: false), "not-depth-65.tmrules.json") },
                notDiagnostics.Add);
            RuleBundle rejectedAll = DeclarativeRuleProvider.LoadBundle(
                new[] { this.WriteSpec(InteractionSpecWithDepth(65, useArray: true), "all-depth-65.tmrules.json") },
                allDiagnostics.Add);

            Assert.AreEqual(1, acceptedNot.Rules.Count);
            Assert.AreEqual(1, acceptedAll.Rules.Count);
            Assert.AreEqual(0, rejectedNot.Rules.Count);
            Assert.AreEqual(0, rejectedAll.Rules.Count);
            Assert.IsTrue(notDiagnostics.Any(message => message.Contains("depth exceeds the limit of 64")));
            Assert.IsTrue(allDiagnostics.Any(message => message.Contains("depth exceeds the limit of 64")));
        }

        /// <summary>
        /// Case-variant v2 markers are routed to strict parsing and rejected rather than silently
        /// loading as legacy rules without pack identity.
        /// </summary>
        [TestMethod]
        public void RejectsCaseVariantVersionTwoMarkers()
        {
            string spec =
                "{\"Schema\":\"tmforge-rules\",\"Version\":2," +
                "\"Dialect\":\"urn:tmforge:rules:flat-v1\"," +
                "\"Pack\":{\"id\":\"case-pack\",\"name\":\"Case pack\"}," +
                "\"Rules\":[{\"id\":\"CASE1\",\"appliesTo\":\"process\",\"message\":\"x\"," +
                "\"when\":{\"property\":\"P\"}}]}";
            List<string> diagnostics = new List<string>();

            RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(
                new[] { this.WriteSpec(spec, "case-variant.tmrules.json") },
                diagnostics.Add);

            Assert.AreEqual(0, bundle.Rules.Count);
            Assert.AreEqual(0, bundle.Packs.Count);
            Assert.IsTrue(diagnostics.Count > 0);
        }

        /// <summary>Declarative help links reject executable and local-resource URI schemes.</summary>
        [TestMethod]
        public void RejectsUnsafeHelpUriSchemes()
        {
            foreach (string scheme in new[] { "javascript:alert(1)", "data:text/html,x", "file:///tmp/help" })
            {
                string rule = V2Rule.Replace(
                    "\"severity\":\"error\"",
                    $"\"severity\":\"error\",\"helpUri\":\"{scheme}\"");
                List<string> diagnostics = new List<string>();

                RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(
                    new[] { this.WriteSpec(VersionTwoSpec("unsafe-help", rule)) },
                    diagnostics.Add);

                Assert.AreEqual(0, bundle.Rules.Count, scheme);
                Assert.IsTrue(diagnostics.Any(message => message.Contains("HTTP or HTTPS")), scheme);
            }
        }

        /// <summary>
        /// Rule files are rejected before parsing when they exceed the input byte limit.
        /// </summary>
        [TestMethod]
        public void RejectsOversizedRuleFile()
        {
            string path = this.WriteSpec(new string(' ', (8 * 1024 * 1024) + 1));
            List<string> diagnostics = new List<string>();

            RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(new[] { path }, diagnostics.Add);

            Assert.AreEqual(0, bundle.Rules.Count);
            Assert.IsTrue(diagnostics.Any(message => message.Contains("File size exceeds")));
        }

        /// <summary>
        /// Legacy files reject null rule entries and use the same per-file text/value limits as v2.
        /// </summary>
        [TestMethod]
        public void LegacyFilesRejectNullEntriesAndExcessiveText()
        {
            string nullPath = this.WriteSpec("{\"rules\":[null]}");
            List<string> nullDiagnostics = new List<string>();

            RuleBundle nullBundle = DeclarativeRuleProvider.LoadBundle(new[] { nullPath }, nullDiagnostics.Add);

            Assert.AreEqual(0, nullBundle.Rules.Count);
            Assert.IsTrue(nullDiagnostics.Any(message => message.Contains("null entries")));

            string longMessage = new string('x', 65537);
            string longPath = this.WriteSpec(
                "{\"rules\":[{\"id\":\"LONG\",\"appliesTo\":\"process\",\"message\":\"" + longMessage +
                "\",\"when\":{\"property\":\"P\"}}]}");
            List<string> longDiagnostics = new List<string>();

            RuleBundle longBundle = DeclarativeRuleProvider.LoadBundle(new[] { longPath }, longDiagnostics.Add);

            Assert.AreEqual(0, longBundle.Rules.Count);
            Assert.IsTrue(longDiagnostics.Any(message => message.Contains("string value exceeds")));
        }

        /// <summary>
        /// Excessive rule and catalog counts are deterministic validation errors.
        /// </summary>
        [TestMethod]
        public void RejectsExcessiveRuleAndCatalogCounts()
        {
            string rule = "{\"id\":\"R$ID$\",\"appliesTo\":\"process\",\"message\":\"x\",\"when\":{\"property\":\"P\"}}";
            string rules = string.Join(",", Enumerable.Range(0, 4097).Select(index => rule.Replace("$ID$", index.ToString())));
            string excessiveRules = VersionTwoSpec("rule-heavy", rules, sourceType: "custom");
            string rulePath = this.WriteSpec(excessiveRules);
            List<string> ruleDiagnostics = new List<string>();

            RuleBundle ruleBundle = DeclarativeRuleProvider.LoadBundle(new[] { rulePath }, ruleDiagnostics.Add);

            Assert.AreEqual(0, ruleBundle.Rules.Count);
            Assert.IsTrue(ruleDiagnostics.Any(message => message.Contains("rule count exceeds")));

            string categories = string.Join(",", Enumerable.Range(0, 513).Select(index => $"{{\"id\":\"C{index}\",\"name\":\"Category {index}\"}}"));
            string excessiveCatalog = VersionTwoSpec("catalog-heavy", V2Rule, categories);
            string catalogPath = this.WriteSpec(excessiveCatalog);
            List<string> catalogDiagnostics = new List<string>();

            RuleBundle catalogBundle = DeclarativeRuleProvider.LoadBundle(new[] { catalogPath }, catalogDiagnostics.Add);

            Assert.AreEqual(0, catalogBundle.Rules.Count);
            Assert.IsTrue(catalogDiagnostics.Any(message => message.Contains("category count exceeds")));
        }

        /// <summary>
        /// Matcher and reference arrays participate in the aggregate catalog-value budget.
        /// </summary>
        [TestMethod]
        public void RejectsExcessiveMatcherValues()
        {
            string values = string.Join(",", Enumerable.Range(0, 65537).Select(index => $"\"V{index}\""));
            string rule =
                "{\"id\":\"VALUES\",\"appliesTo\":\"process\",\"message\":\"x\"," +
                $"\"when\":{{\"property\":\"Cache Type\",\"anyOf\":[{values}]}}}}";
            string path = this.WriteSpec(VersionTwoSpec("value-heavy", rule, sourceType: "custom"));
            List<string> diagnostics = new List<string>();

            RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(new[] { path }, diagnostics.Add);

            Assert.AreEqual(0, bundle.Rules.Count);
            Assert.IsTrue(diagnostics.Any(message => message.Contains("catalog value count exceeds")));
        }

        /// <summary>
        /// A single load is bounded across files, not only within each individually valid pack.
        /// </summary>
        [TestMethod]
        public void RejectsExcessiveSourceFileCount()
        {
            List<string> paths = new List<string>();
            for (int index = 0; index < 129; index++)
            {
                paths.Add(this.WriteSpec("{\"rules\":[]}", $"rules-{index:D3}.tmrules.json"));
            }

            List<string> diagnostics = new List<string>();

            RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(paths, diagnostics.Add);

            Assert.IsTrue(bundle.Rules.Count == 0);
            Assert.IsTrue(diagnostics.Any(message => message.Contains("source file count exceeds")));
        }

        /// <summary>
        /// Ambiguous aliases and cyclic element hierarchies are rejected before rules compile.
        /// </summary>
        [TestMethod]
        public void RejectsAmbiguousCatalogs()
        {
            string spec = VersionTwoSpec("ambiguous", V2Rule)
                .Replace(
                    "{\"id\":\"GE.P\",\"name\":\"Process\",\"parentId\":\"ROOT\"}",
                    "{\"id\":\"GE.P\",\"name\":\"Process\",\"parentId\":\"GE.X\"},{\"id\":\"GE.X\",\"name\":\"Other\",\"parentId\":\"GE.P\"}")
                .Replace(
                    "}] ,\"rules\"",
                    "},{\"name\":\"Other\",\"aliases\":[\"cache-type\"],\"elementTypeIds\":[\"GE.P\"]}] ,\"rules\"");
            string path = this.WriteSpec(spec);
            List<string> diagnostics = new List<string>();

            RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(new[] { path }, diagnostics.Add);

            Assert.AreEqual(0, bundle.Rules.Count);
            Assert.IsTrue(diagnostics.Any(message => message.Contains("cycle") || message.Contains("ambiguous")));
        }

        /// <summary>
        /// Case-insensitive catalog duplicates use one canonical diagnostic regardless of order.
        /// </summary>
        [TestMethod]
        public void DuplicateCatalogDiagnosticsAreOrderIndependent()
        {
            string firstCategories =
                "{\"id\":\"D\",\"name\":\"One\"},{\"id\":\"d\",\"name\":\"Two\"}";
            string secondCategories =
                "{\"id\":\"d\",\"name\":\"Two\"},{\"id\":\"D\",\"name\":\"One\"}";
            string path = this.WriteSpec(
                VersionTwoSpec("catalog-duplicates", V2Rule, firstCategories),
                "catalog-duplicates.tmrules.json");
            List<string> firstDiagnostics = new List<string>();
            _ = DeclarativeRuleProvider.LoadBundle(new[] { path }, firstDiagnostics.Add);

            File.WriteAllText(path, VersionTwoSpec("catalog-duplicates", V2Rule, secondCategories));
            List<string> secondDiagnostics = new List<string>();
            _ = DeclarativeRuleProvider.LoadBundle(new[] { path }, secondDiagnostics.Add);

            Assert.AreEqual(1, firstDiagnostics.Count);
            Assert.AreEqual(firstDiagnostics[0], secondDiagnostics.Single());
            StringAssert.Contains(firstDiagnostics[0], "duplicate category id 'D'");
        }

        /// <summary>
        /// A rule without an id is skipped and reported through diagnostics.
        /// </summary>
        [TestMethod]
        public void SkipsRuleMissingId()
        {
            string spec = "{\"rules\":[{\"appliesTo\":\"datastore\",\"message\":\"x\",\"assert\":{\"property\":\"Encrypted\",\"present\":true}}]}";
            string path = this.WriteSpec(spec);
            List<string> diagnostics = new List<string>();

            IReadOnlyList<Rule> rules = DeclarativeRuleProvider.Load(new[] { path }, diagnostics.Add);

            Assert.AreEqual(0, rules.Count);
            Assert.IsTrue(diagnostics.Any(message => message.Contains("id")));
        }

        /// <summary>
        /// A rule with an unrecognized <c>appliesTo</c> is skipped and reported.
        /// </summary>
        [TestMethod]
        public void SkipsUnknownAppliesTo()
        {
            string spec = "{\"rules\":[{\"id\":\"X1\",\"appliesTo\":\"widget\",\"message\":\"x\",\"assert\":{\"property\":\"P\",\"present\":true}}]}";
            string path = this.WriteSpec(spec);
            List<string> diagnostics = new List<string>();

            IReadOnlyList<Rule> rules = DeclarativeRuleProvider.Load(new[] { path }, diagnostics.Add);

            Assert.AreEqual(0, rules.Count);
            Assert.IsTrue(diagnostics.Any(message => message.Contains("appliesTo")));
        }

        /// <summary>
        /// Relational facets are only valid on flow rules; using one on a non-flow rule is rejected.
        /// </summary>
        [TestMethod]
        public void SkipsRelationalConditionOnNonFlow()
        {
            string spec = "{\"rules\":[{\"id\":\"X1\",\"appliesTo\":\"datastore\",\"message\":\"x\",\"when\":{\"crossesTrustBoundary\":true}}]}";
            string path = this.WriteSpec(spec);
            List<string> diagnostics = new List<string>();

            IReadOnlyList<Rule> rules = DeclarativeRuleProvider.Load(new[] { path }, diagnostics.Add);

            Assert.AreEqual(0, rules.Count);
            Assert.IsTrue(diagnostics.Any(message => message.Contains("flow")));
        }

        /// <summary>
        /// A rule with neither a guard nor a requirement is rejected.
        /// </summary>
        [TestMethod]
        public void SkipsRuleWithoutWhenOrAssert()
        {
            string spec = "{\"rules\":[{\"id\":\"X1\",\"appliesTo\":\"process\",\"message\":\"x\"}]}";
            string path = this.WriteSpec(spec);
            List<string> diagnostics = new List<string>();

            IReadOnlyList<Rule> rules = DeclarativeRuleProvider.Load(new[] { path }, diagnostics.Add);

            Assert.AreEqual(0, rules.Count);
            Assert.IsTrue(diagnostics.Any(message => message.Contains("when") || message.Contains("assert")));
        }

        /// <summary>
        /// A malformed spec file is skipped with a diagnostic rather than throwing.
        /// </summary>
        [TestMethod]
        public void SkipsMalformedJson()
        {
            string path = this.WriteSpec("{ this is not valid json");
            List<string> diagnostics = new List<string>();

            IReadOnlyList<Rule> rules = DeclarativeRuleProvider.Load(new[] { path }, diagnostics.Add);

            Assert.AreEqual(0, rules.Count);
            Assert.AreEqual(1, diagnostics.Count);
        }

        /// <summary>
        /// Tolerant legacy files ignore malformed generic provenance entries instead of aborting the
        /// load; version 2 rejects the same shape through strict pack validation.
        /// </summary>
        [TestMethod]
        public void LegacyRulesIgnoreMalformedProvenanceEntries()
        {
            string spec =
                "{\"rules\":[{\"id\":\"LEGACY-PROVENANCE\",\"appliesTo\":\"process\"," +
                "\"message\":\"x\",\"when\":{\"property\":\"P\"}," +
                "\"provenance\":{\"sourceId\":\"source-rule\",\"expressions\":[null," +
                "{\"role\":\"condition\",\"language\":\"not-namespaced\",\"text\":\"x\"}," +
                "{\"role\":\"condition\",\"language\":\"urn:example:expression\",\"text\":\"valid\"}]}}]}";

            IReadOnlyList<Rule> rules = DeclarativeRuleProvider.Load(new[] { this.WriteSpec(spec) });

            Assert.AreEqual(1, rules.Count);
            Assert.AreEqual("source-rule", rules.Single().Provenance!.SourceId);
            Assert.AreEqual("valid", rules.Single().Provenance!.Expressions.Single().Text);
        }

        private static string VersionTwoSpec(
            string packId,
            string rules,
            string categories = "{\"id\":\"D\",\"name\":\"Denial of Service\"}",
            string sourceType = "mtmt-tb7")
        {
            string source = string.Equals(sourceType, "mtmt-tb7", StringComparison.Ordinal)
                ? "{\"type\":\"urn:tmforge:source:mtmt-tb7\",\"name\":\"Azure Cloud Services.tb7\"," +
                    "\"id\":\"11111111-1111-1111-1111-111111111111\",\"version\":\"1.0.0.33\"}"
                : $"{{\"type\":\"urn:tmforge:source:{sourceType}\"}}";
            return
                "{\"schema\":\"tmforge-rules\",\"version\":2,\"dialect\":\"urn:tmforge:rules:flat-v1\"," +
                $"\"pack\":{{\"id\":\"{packId}\",\"name\":\"Azure Template\",\"source\":{source}}}," +
                $"\"categories\":[{categories}]," +
                "\"elementTypes\":[{\"id\":\"GE.P\",\"name\":\"Process\",\"parentId\":\"ROOT\"}]," +
                "\"properties\":[{\"name\":\"Cache Type\",\"aliases\":[\"cacheType\",\"cache-type\"]," +
                "\"allowedValues\":[\"Static\",\"Distributed\"],\"elementTypeIds\":[\"GE.P\"]}] ," +
                $"\"rules\":[{rules}]}}";
        }

        private static string InteractionSpecWithDepth(int depth, bool useArray)
        {
            string expression = "{\"subject\":\"source\",\"type\":\"ROOT\"}";
            for (int current = 1; current < depth; current++)
            {
                expression = useArray
                    ? "{\"allOf\":[" + expression + "]}"
                    : "{\"not\":" + expression + "}";
            }

            return
                "{\"schema\":\"tmforge-rules\",\"version\":2," +
                "\"dialect\":\"urn:tmforge:rules:interaction-v1\"," +
                "\"pack\":{\"id\":\"depth-pack\",\"name\":\"Depth pack\"}," +
                "\"rules\":[{\"id\":\"DEPTH\",\"message\":\"depth\",\"expression\":" + expression + "}]}";
        }

        private static T CreateEntity<T>(string typeId, string genericTypeId, string name)
            where T : Entity, new()
        {
            T entity = new T
            {
                Guid = Guid.NewGuid(),
                TypeId = typeId,
                GenericTypeId = genericTypeId,
            };
            entity.Properties.Add(new StringDisplayAttribute { Name = "Name", DisplayName = "Name", Value = name });
            return entity;
        }

        private string WriteSpec(string json, string? fileName = null)
        {
            string path = Path.Join(
                this.WorkingDirectory,
                fileName ?? Guid.NewGuid().ToString("N") + ".tmrules.json");
            File.WriteAllText(path, json);
            return path;
        }
    }
}
