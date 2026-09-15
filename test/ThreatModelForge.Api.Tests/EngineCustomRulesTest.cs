namespace ThreatModelForge.Api.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
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

        /// <summary>New predicates preserve finding/threat identities, reports, and exported model semantics.</summary>
        /// <param name="format">The round-trip format.</param>
        [TestMethod]
        [DataRow("tmforge-json")]
        [DataRow("tm7")]
        public void AdditionalMatchersPreserveResultsAcrossFormats(string format)
        {
            (TmForgeModelDto model, EngineRuleOptions rules) = AdditionalMatchers();
            AnalysisResultDto original = EngineService.RunAnalysis(model, rules);
            Assert.AreEqual(0, original.Diagnostics.Count);
            FindingDto[] findings = original.Findings.Where(finding => finding.RuleId?.StartsWith("rule005/", StringComparison.Ordinal) == true).ToArray();
            ThreatDto[] threats = original.Threats.Where(threat => threat.RuleId?.StartsWith("rule005/", StringComparison.Ordinal) == true).ToArray();
            CollectionAssert.AreEquivalent(
                new[] { "rule005/RETENTION", "rule005/SERVICE-NAME", "rule005/AUDIT-PATH" },
                findings.Select(finding => finding.RuleId).ToArray());
            Assert.AreEqual(3, threats.Length);
            Assert.IsTrue(threats.All(threat => threat.CategoryId == "rule005/policy" && threat.Priority == "High"));

            byte[] bytes = EngineService.Convert(model, format, rules);
            TmForgeModelDto restored = EngineService.ReadModel(bytes, format);
            AnalysisResultDto roundTrip = EngineService.RunAnalysis(restored, rules);
            CollectionAssert.AreEquivalent(findings.Select(finding => finding.Id).ToArray(), roundTrip.Findings
                .Where(finding => finding.RuleId?.StartsWith("rule005/", StringComparison.Ordinal) == true).Select(finding => finding.Id).ToArray());
            CollectionAssert.AreEquivalent(threats.Select(threat => threat.Id).ToArray(), roundTrip.Threats
                .Where(threat => threat.RuleId?.StartsWith("rule005/", StringComparison.Ordinal) == true).Select(threat => threat.Id).ToArray());
            Assert.IsFalse(roundTrip.Findings.Any(finding => finding.Id == "engine-error"));
            Assert.AreEqual(original.RulePacks.Single().Fingerprint, roundTrip.RulePacks.Single().Fingerprint);
            string html = Encoding.UTF8.GetString(EngineService.Report(model, "html", rules));
            StringAssert.Contains(html, "retention between 1 and 30 days");
            StringAssert.Contains(html, "service name beginning with svc-");
            StringAssert.Contains(html, "lacks a direct audit-store connection");
        }

        /// <summary>Existing per-rule and per-pack toggles apply to every new matcher.</summary>
        /// <param name="disablePack">Whether to disable the whole pack.</param>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void AdditionalMatchersHonorDisabledSelections(bool disablePack)
        {
            (TmForgeModelDto model, EngineRuleOptions rules) = AdditionalMatchers();
            TmForgeModelDto selected = new TmForgeModelDto
            {
                Elements = model.Elements,
                Flows = model.Flows,
                Analysis = new TmForgeAnalysisDto
                {
                    DisabledPacks = disablePack ? new[] { "rule005" } : Array.Empty<string>(),
                    DisabledRuleIds = disablePack ? Array.Empty<string>() : new[] { "rule005/RETENTION" },
                },
            };
            AnalysisResultDto result = EngineService.RunAnalysis(selected, rules);
            Assert.AreEqual(disablePack ? 0 : 2, result.Findings.Count(finding => finding.RuleId?.StartsWith("rule005/", StringComparison.Ordinal) == true));
            Assert.IsFalse(result.Findings.Any(finding => finding.RuleId == "rule005/RETENTION" || finding.Id == "engine-error"));
        }

        /// <summary>A regex timeout is a visible failure in both projections, not an empty successful analysis.</summary>
        [TestMethod]
        public void RegexTimeoutIsVisibleOnTheEngineFacade()
        {
            EngineRuleOptions rules = new EngineRuleOptions
            {
                Sources = new[]
                {
                    new RuleSourceDto
                    {
                        Name = "timeout.tmrules.json",
                        Json = "{\"rules\":[{\"id\":\"TIMEOUT\",\"appliesTo\":\"process\",\"message\":\"timeout\",\"assert\":{\"property\":\"Value\",\"matches\":\"^(a+)+$\"}}]}",
                    },
                },
            };
            TmForgeModelDto model = new TmForgeModelDto
            {
                Elements = new[]
                {
                    new TmForgeElementDto
                    {
                        Id = "timeout",
                        Kind = "process",
                        Properties = new Dictionary<string, string> { ["Value"] = new string('a', 4095) + "!" },
                    },
                },
            };

            AnalysisResultDto result = EngineService.RunAnalysis(model, rules);

            StringAssert.Contains(result.Findings.Single(finding => finding.Id == "engine-error").Message, "TIMEOUT");
            StringAssert.Contains(result.Threats.Single(threat => threat.Id == "engine-error").Title, "timeout");
            Assert.IsFalse(result.Findings.Any(finding => finding.RuleId == "TIMEOUT"));
        }

        /// <summary>Reads identical rule content and model input for direct-engine and HTTP parity checks.</summary>
        /// <returns>The fixture model and rule sources.</returns>
        internal static (TmForgeModelDto Model, EngineRuleOptions Rules) AdditionalMatchers()
        {
            using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(Path.Join(AppContext.BaseDirectory, "Fixtures", "additional-matchers.json")));
            TmForgeModelDto model = fixture.RootElement.GetProperty("model").Deserialize<TmForgeModelDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new InvalidDataException("The matcher fixture requires a model.");
            EngineRuleOptions rules = new EngineRuleOptions
            {
                Sources = new[] { new RuleSourceDto { Name = "additional-matchers.tmrules.json", Json = fixture.RootElement.GetProperty("pack").GetRawText() } },
            };
            return (model, rules);
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
