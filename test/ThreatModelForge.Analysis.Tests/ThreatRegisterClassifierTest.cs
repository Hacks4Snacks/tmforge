namespace ThreatModelForge.Analysis.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Covers how the threat register is split by origin and by standing against the current analysis.
    /// The register never deletes, so a left-over entry is indistinguishable from a live one until it
    /// is classified; these tests pin when an entry may be called stale and when it may not.
    /// </summary>
    [TestClass]
    public class ThreatRegisterClassifierTest
    {
        /// <summary>An authored entry is counted as manual and is never rule-derived.</summary>
        [TestMethod]
        public void ManualEntryIsClassifiedAsManual()
        {
            ThreatModel model = new ThreatModel();
            AddManualEntry(model, "manual:vendor-access", "Vendor access is unreviewed");
            using RuleSet rules = RuleSetWith("TM1021");

            ThreatRegisterSummary summary = ThreatRegisterClassifier.Classify(model, Generated(), rules);

            Assert.AreEqual(1, summary.Manual);
            Assert.AreEqual(0, summary.PersistedGenerated);
            Assert.AreEqual(0, summary.StaleGenerated);
            Assert.AreEqual(ThreatRegisterStates.Manual, summary.Entries.Single().State);
            Assert.IsNull(summary.Entries.Single().RuleId);
        }

        /// <summary>A stored entry the rules still produce is current, not stale.</summary>
        [TestMethod]
        public void StoredEntryStillProducedIsCurrent()
        {
            ThreatModel model = new ThreatModel();
            string key = AddGeneratedEntry(model, "TM1021", "Audit log is unsigned");
            using RuleSet rules = RuleSetWith("TM1021");

            ThreatRegisterSummary summary = ThreatRegisterClassifier.Classify(model, Generated((key, "TM1021")), rules);

            Assert.AreEqual(1, summary.PersistedGenerated);
            Assert.AreEqual(1, summary.CurrentGenerated);
            Assert.AreEqual(0, summary.StaleGenerated);
            Assert.AreEqual(ThreatRegisterStates.CurrentGenerated, summary.Entries.Single().State);
        }

        /// <summary>
        /// A stored entry whose rule ran and did not produce it is stale: the finding it recorded has
        /// genuinely gone away. It still counts as persisted, because nothing was deleted.
        /// </summary>
        [TestMethod]
        public void StoredEntryWhoseRuleRanButDidNotFireIsStale()
        {
            ThreatModel model = new ThreatModel();
            AddGeneratedEntry(model, "TM1021", "Audit log is unsigned");
            using RuleSet rules = RuleSetWith("TM1021");

            ThreatRegisterSummary summary = ThreatRegisterClassifier.Classify(model, Generated(), rules);

            Assert.AreEqual(1, summary.PersistedGenerated);
            Assert.AreEqual(1, summary.StaleGenerated);
            Assert.AreEqual(0, summary.IndeterminateGenerated);
            Assert.AreEqual(ThreatRegisterStates.StaleGenerated, summary.Entries.Single().State);
        }

        /// <summary>
        /// A stored entry whose rule is not in the effective bundle is reported as indeterminate and
        /// its rule named, never assumed stale. A rule that never ran says nothing about the model, so
        /// calling it stale would invite deleting real findings after a mistyped rule path.
        /// </summary>
        [TestMethod]
        public void StoredEntryWhoseRuleIsMissingIsIndeterminateNotStale()
        {
            ThreatModel model = new ThreatModel();
            AddGeneratedEntry(model, "CORP-1", "Store declares no owner");
            using RuleSet rules = RuleSetWith("TM1021");

            ThreatRegisterSummary summary = ThreatRegisterClassifier.Classify(model, Generated(), rules);

            Assert.AreEqual(0, summary.StaleGenerated, "A rule that never ran cannot make an entry stale.");
            Assert.AreEqual(1, summary.IndeterminateGenerated);
            CollectionAssert.AreEqual(new[] { "CORP-1" }, summary.UnavailableRuleIds.ToArray());
            Assert.AreEqual(ThreatRegisterStates.IndeterminateGenerated, summary.Entries.Single().State);
        }

        /// <summary>A disabled rule is unavailable for this run, so its entries are not stale either.</summary>
        [TestMethod]
        public void StoredEntryWhoseRuleIsDisabledIsIndeterminateNotStale()
        {
            ThreatModel model = new ThreatModel();
            AddGeneratedEntry(model, "TM1021", "Audit log is unsigned");
            using RuleSet rules = RuleSetWith("TM1021");
            rules.Rules.Single().Disabled = true;

            ThreatRegisterSummary summary = ThreatRegisterClassifier.Classify(model, Generated(), rules);

            Assert.AreEqual(0, summary.StaleGenerated);
            Assert.AreEqual(1, summary.IndeterminateGenerated);
            CollectionAssert.AreEqual(new[] { "TM1021" }, summary.UnavailableRuleIds.ToArray());
        }

        /// <summary>
        /// The current-generated count reflects what the rules produce now, which can exceed what the
        /// register stores when the register has not been written since the model changed.
        /// </summary>
        [TestMethod]
        public void CurrentGeneratedCountsWhatTheRulesProduceEvenWhenNothingIsStored()
        {
            ThreatModel model = new ThreatModel();
            using RuleSet rules = RuleSetWith("TM1021");

            ThreatRegisterSummary summary = ThreatRegisterClassifier.Classify(
                model,
                Generated(("aaaa:TM1021", "TM1021"), ("bbbb:TM1021", "TM1021")),
                rules);

            Assert.AreEqual(2, summary.CurrentGenerated);
            Assert.AreEqual(0, summary.PersistedGenerated);
            Assert.AreEqual(0, summary.StaleGenerated);
        }

        /// <summary>
        /// A stale entry that carries triage is reported as carrying it, so a reviewer is told a
        /// decision is attached before anything is removed.
        /// </summary>
        [TestMethod]
        public void StaleEntryReportsThatItCarriesTriage()
        {
            ThreatModel model = new ThreatModel();
            string key = AddGeneratedEntry(model, "TM1021", "Audit log is unsigned");
            model.AllThreatsDictionary[key].State = ThreatState.NotApplicable;
            using RuleSet rules = RuleSetWith("TM1021");

            ThreatRegisterSummary summary = ThreatRegisterClassifier.Classify(model, Generated(), rules);

            ThreatRegisterEntry entry = summary.Entries.Single();
            Assert.AreEqual(ThreatRegisterStates.StaleGenerated, entry.State);
            Assert.IsTrue(entry.HasTriage);
            Assert.AreEqual(ThreatState.NotApplicable, entry.Triage);
        }

        /// <summary>Manual and generated entries are split even when both are present.</summary>
        [TestMethod]
        public void ManualAndGeneratedEntriesAreCountedSeparately()
        {
            ThreatModel model = new ThreatModel();
            AddManualEntry(model, "manual:vendor-access", "Vendor access is unreviewed");
            string live = AddGeneratedEntry(model, "TM1021", "Audit log is unsigned");
            AddGeneratedEntry(model, "TM1029", "No audit trail");
            using RuleSet rules = RuleSetWith("TM1021", "TM1029");

            ThreatRegisterSummary summary = ThreatRegisterClassifier.Classify(model, Generated((live, "TM1021")), rules);

            Assert.AreEqual(1, summary.Manual);
            Assert.AreEqual(2, summary.PersistedGenerated);
            Assert.AreEqual(1, summary.CurrentGenerated);
            Assert.AreEqual(1, summary.StaleGenerated);
            Assert.AreEqual(3, summary.Entries.Count);
        }

        private static void AddManualEntry(ThreatModel model, string id, string title)
        {
            model.AllThreatsDictionary[id] = new Threat
            {
                Id = model.AllThreatsDictionary.Count + 1,
                InteractionKey = id,
                State = ThreatState.AutoGenerated,
                Title = title,
                UserThreatCategory = "Repudiation",
            };
        }

        private static string AddGeneratedEntry(ThreatModel model, string ruleId, string title)
        {
            string key = Guid.NewGuid().ToString("N") + ":" + ruleId;
            model.AllThreatsDictionary[key] = new Threat
            {
                Id = model.AllThreatsDictionary.Count + 1,
                InteractionKey = key,
                TypeId = ruleId,
                State = ThreatState.AutoGenerated,
                Title = title,
                UserThreatCategory = "Repudiation",
            };
            return key;
        }

        private static GenerationResult Generated(params (string Key, string RuleId)[] threats)
        {
            List<GeneratedThreat> produced = threats
                .Select(threat => new GeneratedThreat { Id = threat.Key, RuleId = threat.RuleId })
                .ToList();
            return new GenerationResult(produced);
        }

        private static RuleSet RuleSetWith(params string[] ruleIds)
        {
            RuleSet rules = new RuleSet();
            foreach (string ruleId in ruleIds)
            {
                rules.Rules.Add(new StubRule(ruleId));
            }

            return rules;
        }

        private sealed class StubRule : Rule
        {
            public StubRule(string ruleId)
                : base(ruleId, MessageSeverity.Warning, "test")
            {
            }

            public override void Evaluate(RuleEvaluationContext context)
            {
            }
        }
    }
}
