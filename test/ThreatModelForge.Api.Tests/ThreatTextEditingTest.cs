namespace ThreatModelForge.Api.Tests
{
    using System;
    using System.Linq;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Engine;

    /// <summary>
    /// Tests for author-owned threat text: the title an author may override on any threat, and the
    /// category only a manually authored threat has.
    /// </summary>
    [TestClass]
    public class ThreatTextEditingTest
    {
        private const string ModelJson =
            "{\"schema\":\"tmforge-json\",\"version\":\"0.1\"," +
            "\"elements\":[" +
            "{\"id\":\"11111111-1111-1111-1111-111111111111\",\"kind\":\"datastore\",\"name\":\"Ledger\",\"x\":0,\"y\":0," +
            "\"properties\":{\"StoresLogData\":\"Yes\"}}," +
            "{\"id\":\"22222222-2222-2222-2222-222222222222\",\"kind\":\"process\",\"name\":\"Checkout\",\"x\":200,\"y\":0}]," +
            "\"flows\":[{\"id\":\"33333333-3333-3333-3333-333333333333\"," +
            "\"source\":\"22222222-2222-2222-2222-222222222222\",\"target\":\"11111111-1111-1111-1111-111111111111\",\"name\":\"write\"}]}";

        /// <summary>
        /// An author can retitle a generated threat, and doing so does not move its identity. The id is
        /// what every other artifact reconciles on, so a title edit that renamed the threat would break
        /// the analysis document, the SARIF fingerprint, and the register key at once.
        /// </summary>
        [TestMethod]
        public void RetitlingAGeneratedThreatDoesNotChangeItsId()
        {
            TmForgeModelDto model = Read();
            ThreatDto original = EngineService.GenerateThreats(model).First();

            AuthoringResultDto edited = AuthoringService.EditThreat(
                model,
                new EditThreatRequest { Id = original.Id, Title = "Ledger writes are unauthenticated" });

            Assert.IsTrue(edited.Success, edited.Error);
            ThreatDto after = EngineService.GenerateThreats(edited.Model!).Single(threat => threat.Id == original.Id);
            Assert.AreEqual("Ledger writes are unauthenticated", after.Title);
            Assert.AreEqual(original.RuleId, after.RuleId);
            Assert.AreEqual(original.Category, after.Category);
        }

        /// <summary>
        /// The regression that motivated the override marker: an overridden title has to survive the
        /// register being regenerated, which is what a <c>.tm7</c> export does. Without it the edit
        /// held in memory and reverted the moment the model made a round trip.
        /// </summary>
        [TestMethod]
        public void ATitleOverrideSurvivesExportAndRegeneration()
        {
            TmForgeModelDto model = Read();
            ThreatDto original = EngineService.GenerateThreats(model).First();
            AuthoringResultDto edited = AuthoringService.EditThreat(
                model,
                new EditThreatRequest { Id = original.Id, Title = "Ledger writes are unauthenticated" });
            Assert.IsTrue(edited.Success, edited.Error);

            TmForgeModelDto reread = EngineService.ReadModel(EngineService.Convert(edited.Model!, "tm7"), "tm7");

            ThreatStateDto entry = reread.Threats!.Single(state => state.Id == original.Id);
            Assert.AreEqual("Ledger writes are unauthenticated", entry.Title, "the override must be recorded as author-owned");
            Assert.AreNotEqual(true, entry.Manual, "an overridden rule threat is still a rule threat");

            ThreatDto after = EngineService.GenerateThreats(reread).Single(threat => threat.Id == original.Id);
            Assert.AreEqual("Ledger writes are unauthenticated", after.Title);
        }

        /// <summary>
        /// A threat nobody retitled carries no overlay entry, so the common case stays out of the file
        /// and a model that was only analyzed is not reported as edited.
        /// </summary>
        [TestMethod]
        public void AnUntouchedGeneratedThreatIsNotRecordedAsEdited()
        {
            TmForgeModelDto reread = EngineService.ReadModel(EngineService.Convert(Read(), "tm7"), "tm7");

            Assert.IsNull(reread.Threats, "no threat was edited, so nothing is author-owned");
        }

        /// <summary>
        /// Clearing an override returns the threat to the wording its rule produces, which is what makes
        /// the edit reversible rather than a one-way door.
        /// </summary>
        [TestMethod]
        public void ClearingATitleRestoresTheRuleWording()
        {
            TmForgeModelDto model = Read();
            ThreatDto original = EngineService.GenerateThreats(model).First();

            AuthoringResultDto edited = AuthoringService.EditThreat(
                model,
                new EditThreatRequest { Id = original.Id, Title = "Something else" });
            AuthoringResultDto cleared = AuthoringService.EditThreat(
                edited.Model,
                new EditThreatRequest { Id = original.Id, Title = string.Empty });

            Assert.IsTrue(cleared.Success, cleared.Error);
            ThreatDto after = EngineService.GenerateThreats(cleared.Model!).Single(threat => threat.Id == original.Id);
            Assert.AreEqual(original.Title, after.Title);
        }

        /// <summary>
        /// A generated threat's category is the rule's conclusion. Refusing the edit is the point: an
        /// author who could change it would be recording a claim the analysis does not support, and
        /// silently dropping it would be worse still.
        /// </summary>
        [TestMethod]
        public void TheCategoryOfAGeneratedThreatCannotBeEdited()
        {
            TmForgeModelDto model = Read();
            ThreatDto original = EngineService.GenerateThreats(model).First();

            AuthoringResultDto edited = AuthoringService.EditThreat(
                model,
                new EditThreatRequest { Id = original.Id, Category = "Tampering" });

            Assert.IsFalse(edited.Success);
            StringAssert.Contains(edited.Error, "belongs to the rule");
        }

        /// <summary>A manually authored threat owns both its title and its category.</summary>
        [TestMethod]
        public void AManualThreatOwnsItsTitleAndCategory()
        {
            AuthoringResultDto added = AuthoringService.AddThreat(
                null,
                new AddThreatRequest { Id = "stolen-laptop", Title = "Stolen laptop", Category = "Spoofing" });
            Assert.IsTrue(added.Success, added.Error);

            AuthoringResultDto edited = AuthoringService.EditThreat(
                added.Model,
                new EditThreatRequest { Id = added.Id!, Title = "Unattended laptop", Category = "Tampering" });

            Assert.IsTrue(edited.Success, edited.Error);
            ThreatStateDto entry = edited.Model!.Threats!.Single();
            Assert.AreEqual("Unattended laptop", entry.Title);
            Assert.AreEqual("Tampering", entry.Category);
        }

        /// <summary>
        /// Clearing a manual threat's title is refused rather than accepted, because there is no
        /// generated title underneath it to fall back to and the threat would be left nameless.
        /// </summary>
        [TestMethod]
        public void AManualThreatCannotHaveItsTitleCleared()
        {
            AuthoringResultDto added = AuthoringService.AddThreat(
                null,
                new AddThreatRequest { Id = "stolen-laptop", Title = "Stolen laptop", Category = "Spoofing" });

            AuthoringResultDto edited = AuthoringService.EditThreat(
                added.Model,
                new EditThreatRequest { Id = added.Id!, Title = "   " });

            Assert.IsFalse(edited.Success);
            StringAssert.Contains(edited.Error, "needs a title");
        }

        private static TmForgeModelDto Read()
            => EngineService.ReadModel(Encoding.UTF8.GetBytes(ModelJson), "tmforge-json");
    }
}
