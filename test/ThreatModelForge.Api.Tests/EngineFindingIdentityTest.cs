namespace ThreatModelForge.Api.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Engine;

    /// <summary>
    /// Tests for the stable identity of analysis findings.
    /// </summary>
    /// <remarks>
    /// A finding id is only useful if it means the same thing in two different runs. The scheme this
    /// replaces numbered findings by their position in the message list, so enabling one rule
    /// renumbered every finding after it and no caller could carry a decision — a suppression, a
    /// triage note, an external ledger row — from one analysis to the next. These tests pin the
    /// properties that make reconciliation possible.
    /// </remarks>
    [TestClass]
    public class EngineFindingIdentityTest
    {
        private const string PackJson =
            "{\"schema\":\"tmforge-rules\",\"version\":2,\"dialect\":\"urn:tmforge:rules:flat-v1\"," +
            "\"pack\":{\"id\":\"corporate\",\"name\":\"Corporate baseline\",\"version\":\"2.1\"}," +
            "\"categories\":[{\"id\":\"privacy\",\"name\":\"Privacy\"}]," +
            "\"elementTypes\":[{\"id\":\"GE.DS\",\"name\":\"Data store\",\"parentId\":\"ROOT\"}]," +
            "\"properties\":[{\"name\":\"Encrypted\",\"allowedValues\":[\"No\",\"At-rest\"],\"elementTypeIds\":[\"GE.DS\"]}]," +
            "\"rules\":[{\"id\":\"CORP-1\",\"severity\":\"error\",\"categoryId\":\"privacy\"," +
            "\"appliesTo\":\"datastore\",\"message\":\"{name} must encrypt data at rest.\"," +
            "\"helpText\":\"Set Encrypted to At-rest.\"," +
            "\"assert\":{\"property\":\"Encrypted\",\"equals\":\"At-rest\"}}]}";

        /// <summary>
        /// The acceptance criterion: enabling an unrelated rule pack adds its findings without
        /// disturbing the identity of the findings that were already there.
        /// </summary>
        [TestMethod]
        public void EnablingAnotherRuleDoesNotRenumberExistingFindings()
        {
            TmForgeModelDto model = SampleModel();

            IReadOnlyList<string> before = BuiltInIds(EngineService.Analyze(model, null).Findings);
            AnalysisResultDto after = EngineService.Analyze(model, Rules());

            Assert.IsTrue(
                after.Findings.Any(finding => finding.RuleId == "corporate/CORP-1"),
                "The custom pack must actually contribute a finding, or this proves nothing.");
            CollectionAssert.AreEqual(
                before.ToArray(),
                BuiltInIds(after.Findings).ToArray(),
                "Built-in finding ids changed when an unrelated pack was enabled.");
        }

        /// <summary>
        /// Two analyses of the same model agree. The model deliberately uses author-chosen element ids
        /// rather than guids, because those are re-keyed to fresh guids on every load: an identity
        /// built on the internal guid would differ on every run.
        /// </summary>
        [TestMethod]
        public void RepeatedAnalysisProducesTheSameIds()
        {
            TmForgeModelDto model = SampleModel();

            IReadOnlyList<FindingDto> first = EngineService.Analyze(model, null).Findings;
            IReadOnlyList<FindingDto> second = EngineService.Analyze(model, null).Findings;

            CollectionAssert.AreEqual(
                first.Select(finding => finding.Id).ToArray(),
                second.Select(finding => finding.Id).ToArray());
        }

        /// <summary>An identity has to be unique, or two findings reconcile onto one row.</summary>
        [TestMethod]
        public void FindingIdsAreUniqueWithinARun()
        {
            IReadOnlyList<FindingDto> findings = EngineService.Analyze(SampleModel(), Rules()).Findings;

            Assert.AreEqual(
                findings.Count,
                findings.Select(finding => finding.Id).Distinct(StringComparer.Ordinal).Count());
        }

        /// <summary>
        /// The identity names the rule and the element the caller was told about, so a finding can be
        /// traced back to its subject without consulting the message text.
        /// </summary>
        [TestMethod]
        public void FindingIdNamesItsRuleAndTarget()
        {
            FindingDto finding = EngineService.Analyze(SampleModel(), Rules()).Findings
                .First(entry => entry.RuleId == "corporate/CORP-1");

            Assert.AreEqual("s1", finding.ElementIds.Single());
            Assert.IsTrue(
                finding.Id.StartsWith("corporate/CORP-1:", StringComparison.Ordinal),
                $"Expected the rule id to lead the identity but got '{finding.Id}'.");
            Assert.IsTrue(
                finding.Id.EndsWith(":s1:0", StringComparison.Ordinal),
                $"Expected the target id to be part of the identity but got '{finding.Id}'.");
        }

        /// <summary>
        /// The same diagram-scoped rule firing on two pages produces two identities, keyed by the
        /// author's page id. Page ids that are not guid-shaped are re-keyed on load like element ids,
        /// so this also proves the page's own id reaches the identity.
        /// </summary>
        [TestMethod]
        public void DiagramScopedFindingsAreKeyedByPage()
        {
            TmForgeModelDto model = new TmForgeModelDto
            {
                Diagrams = new[]
                {
                    Page("p1", "Page one", "a1"),
                    Page("p2", "Page two", "b1"),
                },
            };

            IReadOnlyList<string> ids = EngineService.Analyze(model, null).Findings
                .Where(finding => finding.RuleId == "TM1005")
                .Select(finding => finding.Id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            CollectionAssert.AreEqual(new[] { "TM1005:p1:model:0", "TM1005:p2:model:0" }, ids.ToArray());
        }

        /// <summary>
        /// Disabling a rule removes its findings and leaves every other identity alone. This is the
        /// sharpest form of the criterion: the disabled rule is whichever one reported first, so under
        /// a positional scheme every surviving finding would shift by at least one.
        /// </summary>
        [TestMethod]
        public void DisablingARuleDoesNotRenumberTheRemainingFindings()
        {
            IReadOnlyList<FindingDto> all = EngineService.Analyze(SampleModel(), null).Findings;
            string first = all[0].RuleId!;
            Assert.IsTrue(
                all.Any(finding => finding.RuleId != first),
                "The fixture must report more than one rule, or nothing could shift.");

            TmForgeModelDto filtered = SampleModel();
            filtered = new TmForgeModelDto
            {
                Elements = filtered.Elements,
                Flows = filtered.Flows,
                Analysis = new TmForgeAnalysisDto { DisabledRuleIds = new[] { first } },
            };

            IReadOnlyList<FindingDto> remaining = EngineService.Analyze(filtered, null).Findings;

            CollectionAssert.AreEqual(
                all.Where(finding => finding.RuleId != first).Select(finding => finding.Id).ToArray(),
                remaining.Select(finding => finding.Id).ToArray());
        }

        private static IReadOnlyList<string> BuiltInIds(IEnumerable<FindingDto> findings)
        {
            return findings
                .Where(finding => finding.RuleId != null && finding.RuleId.StartsWith("TM", StringComparison.Ordinal))
                .Select(finding => finding.Id)
                .ToList();
        }

        private static EngineRuleOptions Rules()
        {
            return new EngineRuleOptions
            {
                Sources = new[] { new RuleSourceDto { Name = "corporate.tmrules.json", Json = PackJson } },
            };
        }

        private static TmForgeDiagramDto Page(string id, string name, string elementId)
        {
            return new TmForgeDiagramDto
            {
                Id = id,
                Name = name,
                Elements = new[]
                {
                    new TmForgeElementDto { Id = elementId, Kind = "process", Name = "Service " + elementId },
                },
            };
        }

        private static TmForgeModelDto SampleModel()
        {
            return new TmForgeModelDto
            {
                Elements = new[]
                {
                    new TmForgeElementDto
                    {
                        Id = "s1",
                        Kind = "datastore",
                        Name = "Ledger",
                        Properties = new Dictionary<string, string> { ["Encrypted"] = "No" },
                    },
                    new TmForgeElementDto { Id = "p1", Kind = "process", Name = "Checkout" },
                    new TmForgeElementDto { Id = "e1", Kind = "external", Name = "Customer" },
                },
                Flows = new[]
                {
                    new TmForgeFlowDto { Id = "f1", Source = "e1", Target = "p1", Name = "order" },
                    new TmForgeFlowDto { Id = "f2", Source = "p1", Target = "s1", Name = "write" },
                },
            };
        }
    }
}
