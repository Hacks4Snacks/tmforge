namespace ThreatModelForge.Api.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Engine;

    /// <summary>
    /// Contract tests for custom rule content on the shared engine facade. Every transport — the CLI,
    /// the HTTP API, the in-browser engine, and the MCP server — reaches the rule set through this
    /// facade, so proving that one pack yields the same catalog entry, finding, threat, report, and
    /// export here proves it for all of them by construction.
    /// </summary>
    [TestClass]
    public class EngineCustomRulesTest
    {
        private const string PackJson =
            "{\"schema\":\"tmforge-rules\",\"version\":2,\"dialect\":\"urn:tmforge:rules:flat-v1\"," +
            "\"pack\":{\"id\":\"corporate\",\"name\":\"Corporate baseline\",\"version\":\"2.1\"}," +
            "\"categories\":[{\"id\":\"privacy\",\"name\":\"Privacy\"}]," +
            "\"elementTypes\":[{\"id\":\"GE.DS\",\"name\":\"Data store\",\"parentId\":\"ROOT\"}]," +
            "\"properties\":[{\"name\":\"Encrypted\",\"allowedValues\":[\"No\",\"At-rest\"],\"elementTypeIds\":[\"GE.DS\"]}]," +
            "\"rules\":[{\"id\":\"CORP-1\",\"severity\":\"error\",\"categoryId\":\"privacy\",\"defaultPriority\":\"High\"," +
            "\"appliesTo\":\"datastore\",\"message\":\"{name} must encrypt data at rest.\"," +
            "\"helpText\":\"Set Encrypted to At-rest.\"," +
            "\"assert\":{\"property\":\"Encrypted\",\"equals\":\"At-rest\"}}]}";

        private const string EffectiveRuleId = "corporate/CORP-1";

        /// <summary>The custom pack contributes to the rule catalog every surface reads.</summary>
        [TestMethod]
        public void CustomPackAppearsInTheRuleCatalog()
        {
            IReadOnlyList<RuleDto> builtIn = EngineService.GetRules();
            IReadOnlyList<RuleDto> effective = EngineService.GetRules(Rules());

            Assert.IsFalse(builtIn.Any(rule => rule.Id == EffectiveRuleId));
            RuleDto custom = effective.Single(rule => rule.Id == EffectiveRuleId);
            Assert.AreEqual("corporate", custom.Pack);
            Assert.AreEqual("error", custom.Severity);
            Assert.AreEqual(builtIn.Count + 1, effective.Count);
        }

        /// <summary>The custom pack is selectable, so per-model toggles cover imported packs too.</summary>
        [TestMethod]
        public void CustomPackAppearsInTheRulePackCatalog()
        {
            RulePackDto pack = EngineService.GetRulePacks(Rules()).Single(entry => entry.Id == "corporate");

            Assert.AreEqual("Corporate baseline", pack.Name);
            Assert.AreEqual(1, pack.Count);
        }

        /// <summary>
        /// Analysis runs the custom rule and reports the pack identity and content fingerprint that
        /// produced the findings, so a caller can tell which rules actually ran.
        /// </summary>
        [TestMethod]
        public void AnalyzeRunsTheCustomRuleAndReportsTheEffectivePack()
        {
            AnalysisResultDto result = EngineService.Analyze(UnencryptedStoreModel(), Rules());

            FindingDto finding = result.Findings.Single(entry => entry.RuleId == EffectiveRuleId);
            Assert.AreEqual("error", finding.Severity);
            Assert.IsTrue(finding.Message.Contains("Ledger", StringComparison.Ordinal));

            RulePackInfoDto pack = result.RulePacks.Single();
            Assert.AreEqual("corporate", pack.Id);
            Assert.AreEqual("2.1", pack.Version);
            Assert.AreEqual(1, pack.RuleCount);
            Assert.IsTrue(pack.Fingerprint.StartsWith("sha256:", StringComparison.Ordinal));
            Assert.AreEqual(0, result.Diagnostics.Count);
        }

        /// <summary>Without the pack the same model produces no such finding: the rule is opt-in.</summary>
        [TestMethod]
        public void AnalyzeWithoutTheCustomPackDoesNotRunTheRule()
        {
            IReadOnlyList<FindingDto> findings = EngineService.Analyze(UnencryptedStoreModel());

            Assert.IsFalse(findings.Any(finding => finding.RuleId == EffectiveRuleId));
        }

        /// <summary>
        /// A threat-bearing custom rule projects into the register with the pack's category and
        /// declared priority, exactly as a built-in threat-bearing rule does.
        /// </summary>
        [TestMethod]
        public void GenerateThreatsProjectsTheCustomRule()
        {
            IReadOnlyList<ThreatDto> threats = EngineService.GenerateThreats(UnencryptedStoreModel(), Rules());

            ThreatDto threat = threats.Single(entry => entry.RuleId == EffectiveRuleId);
            Assert.AreEqual("corporate/privacy", threat.CategoryId);
            Assert.AreEqual("Privacy", threat.CategoryName);
            Assert.AreEqual("High", threat.Priority);
        }

        /// <summary>Disabling the custom pack suppresses it exactly as it does a built-in pack.</summary>
        [TestMethod]
        public void DisablingTheCustomPackSuppressesIt()
        {
            TmForgeModelDto model = UnencryptedStoreModel();
            TmForgeModelDto disabled = new TmForgeModelDto
            {
                Elements = model.Elements,
                Analysis = new TmForgeAnalysisDto { DisabledPacks = new[] { "corporate" } },
            };

            AnalysisResultDto result = EngineService.Analyze(disabled, Rules());

            Assert.IsFalse(result.Findings.Any(finding => finding.RuleId == EffectiveRuleId));
            Assert.AreEqual("corporate", result.RulePacks.Single().Id);
        }

        /// <summary>
        /// The custom rule's threat reaches the rendered report, so the report a reviewer reads matches
        /// the analysis they ran.
        /// </summary>
        [TestMethod]
        public void ReportIncludesTheCustomRuleThreat()
        {
            string html = Encoding.UTF8.GetString(EngineService.Report(UnencryptedStoreModel(), "html", Rules()));

            Assert.IsTrue(html.Contains("must encrypt data at rest", StringComparison.Ordinal));
        }

        /// <summary>
        /// The register-bearing <c>.tm7</c> export carries the custom rule's threat, so the pack's
        /// findings survive a round trip through the lossless format.
        /// </summary>
        [TestMethod]
        public void Tm7ExportCarriesTheCustomRuleThreat()
        {
            string document = Encoding.UTF8.GetString(EngineService.ExportTm7(UnencryptedStoreModel(), Rules()));

            Assert.IsTrue(document.Contains("must encrypt data at rest", StringComparison.Ordinal));
        }

        /// <summary>
        /// A model that pins a pack it was reviewed with reports an error when that pack is absent,
        /// rather than looking clean because the rule never ran.
        /// </summary>
        [TestMethod]
        public void MissingExpectedPackIsReportedAsAnError()
        {
            TmForgeModelDto model = UnencryptedStoreModel();
            TmForgeModelDto pinned = new TmForgeModelDto
            {
                Elements = model.Elements,
                Analysis = new TmForgeAnalysisDto
                {
                    ExpectedPacks = new[] { new ExpectedRulePackDto { Id = "corporate" } },
                },
            };

            AnalysisResultDto result = EngineService.Analyze(pinned, null);

            FindingDto finding = result.Findings.Single(entry => entry.RuleId == "rule-pack-mismatch");
            Assert.AreEqual("error", finding.Severity);
            Assert.IsTrue(finding.Message.Contains("corporate", StringComparison.Ordinal));
        }

        /// <summary>Changed pack content is reported: pinning is by fingerprint, not by name.</summary>
        [TestMethod]
        public void ChangedPackContentIsReportedAsAnError()
        {
            TmForgeModelDto model = UnencryptedStoreModel();
            TmForgeModelDto pinned = new TmForgeModelDto
            {
                Elements = model.Elements,
                Analysis = new TmForgeAnalysisDto
                {
                    ExpectedPacks = new[]
                    {
                        new ExpectedRulePackDto { Id = "corporate", Fingerprint = "sha256:stale" },
                    },
                },
            };

            AnalysisResultDto result = EngineService.Analyze(pinned, Rules());

            FindingDto finding = result.Findings.Single(entry => entry.RuleId == "rule-pack-mismatch");
            Assert.IsTrue(finding.Message.Contains("sha256:stale", StringComparison.Ordinal));
        }

        /// <summary>A matching fingerprint passes silently: the pin only speaks up when it is broken.</summary>
        [TestMethod]
        public void MatchingFingerprintProducesNoMismatchFinding()
        {
            AnalysisResultDto probe = EngineService.Analyze(UnencryptedStoreModel(), Rules());
            TmForgeModelDto model = UnencryptedStoreModel();
            TmForgeModelDto pinned = new TmForgeModelDto
            {
                Elements = model.Elements,
                Analysis = new TmForgeAnalysisDto
                {
                    ExpectedPacks = new[]
                    {
                        new ExpectedRulePackDto { Id = "corporate", Fingerprint = probe.RulePacks.Single().Fingerprint },
                    },
                },
            };

            AnalysisResultDto result = EngineService.Analyze(pinned, Rules());

            Assert.IsFalse(result.Findings.Any(finding => finding.RuleId == "rule-pack-mismatch"));
        }

        /// <summary>
        /// A pack that fails to load is reported through diagnostics and contributes no rules, so a
        /// host never presents a broken pack as a working one.
        /// </summary>
        [TestMethod]
        public void MalformedPackIsReportedThroughDiagnostics()
        {
            EngineRuleOptions broken = new EngineRuleOptions
            {
                Sources = new[] { new RuleSourceDto { Name = "broken.tmrules.json", Json = "{ not json" } },
            };

            RuleBundleDto bundle = EngineService.DescribeRules(broken);

            Assert.AreEqual(0, bundle.RulePacks.Count);
            Assert.IsTrue(bundle.Diagnostics.Any(message => message.Contains("broken.tmrules.json", StringComparison.Ordinal)));
        }

        private static EngineRuleOptions Rules()
        {
            return new EngineRuleOptions
            {
                Sources = new[] { new RuleSourceDto { Name = "corporate.tmrules.json", Json = PackJson } },
            };
        }

        private static TmForgeModelDto UnencryptedStoreModel()
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
                },
            };
        }
    }
}
