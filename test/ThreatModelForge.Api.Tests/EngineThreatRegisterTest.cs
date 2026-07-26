namespace ThreatModelForge.Api.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Engine;

    /// <summary>
    /// Covers the engine's threat-register projection: the same origin/standing split the CLI reports,
    /// reached through the shared facade so every surface agrees.
    /// </summary>
    [TestClass]
    public class EngineThreatRegisterTest
    {
        /// <summary>A manual overlay entry is reported as manual and never rule-derived.</summary>
        [TestMethod]
        public void ManualOverlayEntryIsReportedAsManual()
        {
            TmForgeModelDto model = SampleModel(new ThreatStateDto
            {
                Id = "manual:vendor-access",
                Manual = true,
                Category = "Repudiation",
                Title = "Vendor access is unreviewed",
            });

            ThreatRegisterDto register = EngineService.DescribeThreatRegister(model);

            Assert.AreEqual(1, register.Manual);
            ThreatRegisterEntryDto entry = register.Entries.Single(e => e.Id == "manual:vendor-access");
            Assert.AreEqual("manual", entry.State);
            Assert.IsNull(entry.RuleId);
        }

        /// <summary>
        /// The current-generated count reflects what the rules produce, independent of what the overlay
        /// happens to store.
        /// </summary>
        [TestMethod]
        public void CurrentGeneratedReflectsWhatTheRulesProduce()
        {
            ThreatRegisterDto register = EngineService.DescribeThreatRegister(SampleModel());

            Assert.IsTrue(register.CurrentGenerated > 0, "The sample model trips real rules.");
            Assert.AreEqual(0, register.StaleGenerated);
            Assert.AreEqual(0, register.IndeterminateGenerated);
        }

        /// <summary>
        /// Triage recorded against a rule that no longer exists is reported as indeterminate, not stale,
        /// and the rule is named. Orphaned triage is exactly what this surface exists to reveal.
        /// </summary>
        [TestMethod]
        public void TriageForAnUnknownRuleIsIndeterminateAndNamesTheRule()
        {
            TmForgeModelDto model = SampleModel(new ThreatStateDto
            {
                Id = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa:CORP-1",
                State = "Mitigated",
                Title = "Retired corporate rule",
            });

            ThreatRegisterDto register = EngineService.DescribeThreatRegister(model);

            Assert.AreEqual(0, register.StaleGenerated, "A rule that never ran cannot make an entry stale.");
            Assert.AreEqual(1, register.IndeterminateGenerated);
            CollectionAssert.Contains(register.UnavailableRuleIds.ToArray(), "CORP-1");
        }

        /// <summary>The register projection agrees with the threat projection on what the rules produce.</summary>
        [TestMethod]
        public void RegisterAgreesWithTheThreatProjection()
        {
            TmForgeModelDto model = SampleModel();

            ThreatRegisterDto register = EngineService.DescribeThreatRegister(model);
            IReadOnlyList<ThreatDto> threats = EngineService.GenerateThreats(model);

            Assert.AreEqual(
                threats.Count(threat => !threat.Manual),
                register.CurrentGenerated,
                "Both surfaces must describe the same run.");
        }

        private static TmForgeModelDto SampleModel(params ThreatStateDto[] overlay)
        {
            return new TmForgeModelDto
            {
                Elements = new[]
                {
                    new TmForgeElementDto { Id = "11111111-1111-4111-8111-111111111111", Kind = "external", Name = "Client" },
                    new TmForgeElementDto { Id = "22222222-2222-4222-8222-222222222222", Kind = "process", Name = "Gateway" },
                    new TmForgeElementDto { Id = "33333333-3333-4333-8333-333333333333", Kind = "datastore", Name = "Ledger" },
                },
                Flows = new[]
                {
                    new TmForgeFlowDto
                    {
                        Id = "44444444-4444-4444-8444-444444444444",
                        Source = "11111111-1111-4111-8111-111111111111",
                        Target = "22222222-2222-4222-8222-222222222222",
                        Name = "request",
                    },
                },
                Threats = overlay.Length == 0 ? null : overlay,
            };
        }
    }
}
