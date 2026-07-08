namespace ThreatModelForge.Api.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Engine;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for <see cref="EngineService.GenerateThreats"/>, confirming the shared engine seam
    /// projects the same rule findings the CLI does (parity by construction) and honors the per-model
    /// rule selection.
    /// </summary>
    [TestClass]
    public class EngineGenerateThreatsTest
    {
        /// <summary>An unauthenticated external interactor yields a spoofing threat from rule TM1023.</summary>
        [TestMethod]
        public void GenerateThreats_ProducesSpoofingFromRule()
        {
            IReadOnlyList<ThreatDto> threats = EngineService.GenerateThreats(BuildModel(null));

            ThreatDto? spoof = threats.FirstOrDefault(t => t.RuleId == "TM1023");
            Assert.IsNotNull(spoof);
            Assert.AreEqual("Spoofing", spoof!.Category);
            Assert.IsTrue(spoof.References.Contains("CWE-287"));
        }

        /// <summary>Disabling the rule's pack removes its threat — detection and generation share the rule set.</summary>
        [TestMethod]
        public void GenerateThreats_HonorsDisabledPack()
        {
            TmForgeAnalysisDto analysis = new TmForgeAnalysisDto
            {
                DisabledPacks = new[] { "identity-access" },
            };

            IReadOnlyList<ThreatDto> threats = EngineService.GenerateThreats(BuildModel(analysis));

            Assert.IsFalse(threats.Any(t => t.RuleId == "TM1023"));
        }

        /// <summary>A model that carries an acceptance overlay returns that threat as Accepted, others Open.</summary>
        [TestMethod]
        public void GenerateThreats_OverlaysAcceptedTriage()
        {
            // First pass: discover the spoofing threat's register id (it starts open).
            IReadOnlyList<ThreatDto> open = EngineService.GenerateThreats(BuildModel(null));
            ThreatDto spoof = open.First(t => t.RuleId == "TM1023");
            Assert.AreEqual("Open", spoof.State);

            // Second pass: carry an acceptance for that threat on the model's triage overlay.
            ThreatStateDto[] triage = new[]
            {
                new ThreatStateDto { Id = spoof.Id, State = "Accepted", Justification = "Client authenticates upstream." },
            };
            IReadOnlyList<ThreatDto> triaged = EngineService.GenerateThreats(BuildModel(null, triage));

            ThreatDto accepted = triaged.First(t => t.Id == spoof.Id);
            Assert.AreEqual("Accepted", accepted.State);
            Assert.AreEqual("Client authenticates upstream.", accepted.Justification);
            Assert.IsTrue(triaged.Where(t => t.Id != spoof.Id).All(t => t.State == "Open"));
        }

        /// <summary>
        /// Exporting a model that carries an acceptance overlay to <c>.tm7</c> seeds the register with
        /// the accepted threat, so a risk accepted in Studio round-trips natively in the exported file
        /// (the CLI then reads the same register). This closes the cross-surface acceptance gap.
        /// </summary>
        [TestMethod]
        public void ExportTm7_CarriesAcceptedTriage()
        {
            IReadOnlyList<ThreatDto> open = EngineService.GenerateThreats(BuildModel(null));
            ThreatDto spoof = open.First(t => t.RuleId == "TM1023");
            ThreatStateDto[] triage = new[]
            {
                new ThreatStateDto { Id = spoof.Id, State = "Accepted", Justification = "Client authenticates upstream." },
            };

            byte[] tm7 = EngineService.ExportTm7(BuildModel(null, triage));

            ThreatModel model;
            using (MemoryStream stream = new MemoryStream(tm7))
            {
                model = ThreatModel.Load(stream);
            }

            Assert.IsTrue(model.AllThreatsDictionary.TryGetValue(spoof.Id, out Threat? threat));
            Assert.AreEqual(ThreatState.NotApplicable, threat!.State);
            Assert.AreEqual("Client authenticates upstream.", threat.StateInformation);

            // The export materializes the full, titled register (not just the accepted stub): the
            // accepted threat keeps its generated title and type, and the register holds every
            // generated threat so a tool such as MTMT sees a complete analysis.
            Assert.IsFalse(string.IsNullOrEmpty(threat.Title));
            Assert.AreEqual("TM1023", threat.TypeId);
            Assert.AreEqual(open.Count, model.AllThreatsDictionary.Count);
        }

        private static TmForgeModelDto BuildModel(TmForgeAnalysisDto? analysis, IReadOnlyList<ThreatStateDto>? threats = null)
        {
            // Guid-shaped ids so the generated threat ids are stable across builds: the register is
            // keyed by the target element's guid, and tmforge-json preserves guid ids on read (real
            // Studio ids are crypto.randomUUID() guids, so this matches production behavior).
            const string externalId = "11111111-1111-4111-8111-111111111111";
            const string processId = "22222222-2222-4222-8222-222222222222";
            TmForgeElementDto external = new TmForgeElementDto { Id = externalId, Kind = "external", Name = "Client", X = 50, Y = 50 };
            TmForgeElementDto process = new TmForgeElementDto { Id = processId, Kind = "process", Name = "Gateway", X = 220, Y = 50 };
            return new TmForgeModelDto
            {
                Schema = "tmforge-json",
                Version = "0.1",
                Elements = new[] { external, process },
                Flows = new[] { new TmForgeFlowDto { Id = "33333333-3333-4333-8333-333333333333", Source = externalId, Target = processId, Name = "request" } },
                Analysis = analysis,
                Threats = threats,
            };
        }
    }
}
