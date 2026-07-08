namespace ThreatModelForge.Api.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Engine;

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
            TmForgeValidationDto validation = new TmForgeValidationDto
            {
                DisabledPacks = new[] { "identity-access" },
            };

            IReadOnlyList<ThreatDto> threats = EngineService.GenerateThreats(BuildModel(validation));

            Assert.IsFalse(threats.Any(t => t.RuleId == "TM1023"));
        }

        private static TmForgeModelDto BuildModel(TmForgeValidationDto? validation)
        {
            TmForgeElementDto external = new TmForgeElementDto { Id = "e1", Kind = "external", Name = "Client", X = 50, Y = 50 };
            TmForgeElementDto process = new TmForgeElementDto { Id = "p1", Kind = "process", Name = "Gateway", X = 220, Y = 50 };
            return new TmForgeModelDto
            {
                Schema = "tmforge-json",
                Version = "0.1",
                Elements = new[] { external, process },
                Flows = new[] { new TmForgeFlowDto { Id = "f1", Source = "e1", Target = "p1", Name = "request" } },
                Validation = validation,
            };
        }
    }
}
