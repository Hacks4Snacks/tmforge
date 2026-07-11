namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for <see cref="ThreatGenerator"/>: it projects the real rule findings into threats
    /// (no independent detection), persists them idempotently, preserves triage, and records
    /// acceptance.
    /// </summary>
    [TestClass]
    public class ThreatGeneratorTest
    {
        private const string ProcessTypeId = "GE.P";
        private const string ExternalTypeId = "GE.EI";

        /// <summary>An unauthenticated external interactor produces a spoofing threat from rule TM1023.</summary>
        [TestMethod]
        public void GenerateProducesSpoofingThreatFromRule()
        {
            ThreatModel model = UnauthenticatedExternalModel();

            GenerationResult result = ThreatGenerator.Generate(model);

            GeneratedThreat? spoof = result.Threats.FirstOrDefault(t => t.RuleId == "TM1023");
            Assert.IsNotNull(spoof);
            Assert.AreEqual(StrideCategory.Spoofing, spoof!.Category);
            Assert.IsFalse(string.IsNullOrEmpty(spoof.Mitigation));
            Assert.IsTrue(spoof.References.Any(r => r.Id == "CWE-287"));
        }

        /// <summary>Every generated threat comes from a threat-bearing rule; hygiene findings are excluded.</summary>
        [TestMethod]
        public void GeneratedThreatsAreOnlyThreatBearingRules()
        {
            ThreatModel model = UnauthenticatedExternalModel();

            GenerationResult result = ThreatGenerator.Generate(model);

            Assert.IsTrue(result.Count > 0);
            foreach (GeneratedThreat threat in result.Threats)
            {
                // Every generated threat comes from a rule that declares its STRIDE identity, so it
                // carries the rule's references. Hygiene rules (no Stride) never reach here.
                Assert.IsTrue(threat.References.Count > 0, $"Threat {threat.RuleId} should carry its rule's references.");
            }

            // TM1005 (minimum component count) is a hygiene rule and must never surface as a threat.
            Assert.IsFalse(result.Threats.Any(t => t.RuleId == "TM1005"));
            Assert.IsTrue(result.Threats.Any(t => t.RuleId == "TM1023"));
        }

        /// <summary><see cref="ThreatGenerator.Apply"/> persists the threats into the register.</summary>
        [TestMethod]
        public void ApplyPersistsThreats()
        {
            ThreatModel model = UnauthenticatedExternalModel();
            GenerationResult result = ThreatGenerator.Generate(model);

            ApplyResult applied = ThreatGenerator.Apply(model, result);

            Assert.AreEqual(result.Count, applied.Added);
            Assert.AreEqual(result.Count, model.AllThreatsDictionary.Count);
            Assert.IsTrue(model.ThreatGenerationEnabled ?? false);
        }

        /// <summary>Re-running generation and apply is idempotent: nothing is added the second time.</summary>
        [TestMethod]
        public void ApplyIsIdempotent()
        {
            ThreatModel model = UnauthenticatedExternalModel();
            ThreatGenerator.Apply(model, ThreatGenerator.Generate(model));
            int count = model.AllThreatsDictionary.Count;

            ApplyResult second = ThreatGenerator.Apply(model, ThreatGenerator.Generate(model));

            Assert.AreEqual(0, second.Added);
            Assert.AreEqual(count, model.AllThreatsDictionary.Count);
        }

        /// <summary>Re-generation preserves a human-triaged threat rather than overwriting it.</summary>
        [TestMethod]
        public void ApplyPreservesTriagedThreats()
        {
            ThreatModel model = UnauthenticatedExternalModel();
            ThreatGenerator.Apply(model, ThreatGenerator.Generate(model));
            string key = model.AllThreatsDictionary.Keys.First();
            model.AllThreatsDictionary[key].State = ThreatState.Mitigated;

            ApplyResult second = ThreatGenerator.Apply(model, ThreatGenerator.Generate(model));

            Assert.IsTrue(second.Preserved >= 1);
            Assert.AreEqual(ThreatState.Mitigated, model.AllThreatsDictionary[key].State);
        }

        /// <summary>
        /// A sparse triage overlay is enriched with its rule-owned fields without replacing the
        /// accepted state or author-supplied treatment values.
        /// </summary>
        [TestMethod]
        public void ApplyHydratesSparseTriagedThreats()
        {
            ThreatModel model = UnauthenticatedExternalModel();
            GeneratedThreat generated = ThreatGenerator.Generate(model).Threats.First(threat => threat.RuleId == "TM1023");
            model.AllThreatsDictionary[generated.Id] = new Threat
            {
                SourceGuid = Guid.NewGuid(),
                DrawingSurfaceGuid = Guid.NewGuid(),
                State = ThreatState.NotApplicable,
                StateInformation = "Authenticated by the upstream identity proxy.",
                Priority = "Low",
                Properties = new System.Collections.Generic.Dictionary<string, string>
                {
                    ["Mitigation"] = "Maintain the proxy policy.",
                },
            };

            ApplyResult result = ThreatGenerator.Apply(model, ThreatGenerator.Generate(model));

            Threat hydrated = model.AllThreatsDictionary[generated.Id];
            Assert.IsTrue(result.Preserved >= 1);
            Assert.AreEqual(ThreatState.NotApplicable, hydrated.State);
            Assert.AreEqual("Authenticated by the upstream identity proxy.", hydrated.StateInformation);
            Assert.AreEqual("Low", hydrated.Priority);
            Assert.AreEqual("Maintain the proxy policy.", hydrated.Properties!["Mitigation"]);
            Assert.AreEqual(generated.Title, hydrated.Title);
            Assert.AreEqual("TM1023", hydrated.TypeId);
            Assert.AreEqual("Spoofing", hydrated.UserThreatCategory);
            Assert.AreEqual(generated.SourceGuid, hydrated.SourceGuid);
            Assert.AreEqual(generated.DiagramGuid, hydrated.DrawingSurfaceGuid);
            Assert.AreEqual(generated.InteractionString, hydrated.InteractionString);
            StringAssert.Contains(hydrated.Properties["References"], "CWE-287");
        }

        /// <summary>Accepting a threat marks it not-applicable and records the justification.</summary>
        [TestMethod]
        public void AcceptMarksThreatNotApplicableWithJustification()
        {
            ThreatModel model = UnauthenticatedExternalModel();
            ThreatGenerator.Apply(model, ThreatGenerator.Generate(model));
            string key = model.AllThreatsDictionary.Keys.First();

            bool accepted = ThreatGenerator.Accept(model, key, "Handled upstream");

            Assert.IsTrue(accepted);
            Assert.AreEqual(ThreatState.NotApplicable, model.AllThreatsDictionary[key].State);
            Assert.AreEqual("Handled upstream", model.AllThreatsDictionary[key].StateInformation);
        }

        /// <summary>Accepting an unknown identifier reports failure.</summary>
        [TestMethod]
        public void AcceptUnknownThreatReturnsFalse()
        {
            ThreatModel model = UnauthenticatedExternalModel();
            ThreatGenerator.Apply(model, ThreatGenerator.Generate(model));

            Assert.IsFalse(ThreatGenerator.Accept(model, "does-not-exist", "reason"));
        }

        private static ThreatModel UnauthenticatedExternalModel()
        {
            StencilRectangle external = External("Client");
            StencilEllipse process = Process("Gateway");
            Connector flow = Flow(external, process, "request");
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Header = "DFD" };
            diagram.Borders.Add(external.Guid, external);
            diagram.Borders.Add(process.Guid, process);
            diagram.Lines.Add(flow.Guid, flow);
            ThreatModel model = new ThreatModel();
            model.DrawingSurfaceList.Add(diagram);
            return model;
        }

        private static StencilRectangle External(string name)
        {
            StencilRectangle entity = new StencilRectangle { Guid = Guid.NewGuid(), GenericTypeId = ExternalTypeId };
            SetName(entity, name);
            return entity;
        }

        private static StencilEllipse Process(string name)
        {
            StencilEllipse entity = new StencilEllipse { Guid = Guid.NewGuid(), GenericTypeId = ProcessTypeId };
            SetName(entity, name);
            return entity;
        }

        private static Connector Flow(Entity source, Entity target, string name)
        {
            Connector connector = new Connector { Guid = Guid.NewGuid(), SourceGuid = source.Guid, TargetGuid = target.Guid };
            SetName(connector, name);
            return connector;
        }

        private static void SetName(Entity entity, string name)
        {
            entity.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = name });
        }
    }
}
