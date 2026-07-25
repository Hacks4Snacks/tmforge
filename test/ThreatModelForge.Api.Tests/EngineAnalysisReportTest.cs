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
    /// Unit tests for the engine's analysis-report facade: the SARIF, findings HTML, and findings JSON
    /// artifacts every host can now produce. These are the evidence a reviewer or a CI job reads, so
    /// they must carry the effective rules and the model's own analysis selection.
    /// </summary>
    [TestClass]
    public class EngineAnalysisReportTest
    {
        private const string PackJson =
            "{\"schema\":\"tmforge-rules\",\"version\":2,\"dialect\":\"urn:tmforge:rules:flat-v1\"," +
            "\"pack\":{\"id\":\"corporate\",\"name\":\"Corporate baseline\",\"version\":\"2.1\"}," +
            "\"categories\":[{\"id\":\"privacy\",\"name\":\"Privacy\"}]," +
            "\"elementTypes\":[{\"id\":\"GE.DS\",\"name\":\"Data store\",\"parentId\":\"ROOT\"}]," +
            "\"properties\":[{\"name\":\"Encrypted\",\"allowedValues\":[\"No\",\"At-rest\"],\"elementTypeIds\":[\"GE.DS\"]}]," +
            "\"rules\":[{\"id\":\"CORP-1\",\"severity\":\"error\",\"categoryId\":\"privacy\"," +
            "\"appliesTo\":\"datastore\",\"message\":\"{name} must encrypt data at rest.\"," +
            "\"assert\":{\"property\":\"Encrypted\",\"equals\":\"At-rest\"}}]}";

        /// <summary>SARIF output is a valid 2.1.0 log carrying the run's results.</summary>
        [TestMethod]
        public void SarifReportIsAValidLog()
        {
            string sarif = Encoding.UTF8.GetString(EngineService.AnalysisReport(SampleModel(), "sarif"));

            using JsonDocument document = JsonDocument.Parse(sarif);
            JsonElement root = document.RootElement;
            Assert.AreEqual("2.1.0", root.GetProperty("version").GetString());
            JsonElement run = root.GetProperty("runs")[0];
            Assert.IsTrue(run.GetProperty("results").GetArrayLength() > 0);
            Assert.IsFalse(string.IsNullOrEmpty(run.GetProperty("tool").GetProperty("driver").GetProperty("name").GetString()));
        }

        /// <summary>The findings HTML is a self-contained document naming the rules that fired.</summary>
        [TestMethod]
        public void FindingsHtmlIsSelfContained()
        {
            string html = Encoding.UTF8.GetString(EngineService.AnalysisReport(SampleModel(), "html"));

            StringAssert.Contains(html, "<html");
            StringAssert.Contains(html, "TM1002");
        }

        /// <summary>The findings JSON is the same camel-cased report shape the CLI writes.</summary>
        [TestMethod]
        public void FindingsJsonMatchesTheCliShape()
        {
            string json = Encoding.UTF8.GetString(EngineService.AnalysisReport(SampleModel(), "json"));

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            Assert.IsTrue(root.TryGetProperty("ruleReports", out JsonElement rules));
            Assert.IsTrue(rules.GetArrayLength() > 0);
            Assert.IsTrue(root.TryGetProperty("diagramSummaries", out _));
        }

        /// <summary>An unrecognized format falls back to the readable findings document.</summary>
        [TestMethod]
        public void UnknownFormatFallsBackToHtml()
        {
            string report = Encoding.UTF8.GetString(EngineService.AnalysisReport(SampleModel(), "xlsx"));

            StringAssert.Contains(report, "<html");
        }

        /// <summary>A custom pack's rule reaches SARIF, so CI gates on the same rules the author ran.</summary>
        [TestMethod]
        public void CustomRulesReachTheAnalysisReport()
        {
            EngineRuleOptions rules = new EngineRuleOptions
            {
                Sources = new[] { new RuleSourceDto { Name = "corporate.tmrules.json", Json = PackJson } },
            };

            string sarif = Encoding.UTF8.GetString(EngineService.AnalysisReport(UnencryptedStoreModel(), "sarif", rules));

            StringAssert.Contains(sarif, "corporate/CORP-1");
            StringAssert.Contains(sarif, "must encrypt data at rest");
        }

        /// <summary>The model's disabled selection is honored, so a report matches its analysis.</summary>
        [TestMethod]
        public void DisabledPacksAreHonored()
        {
            TmForgeModelDto model = SampleModel();
            TmForgeModelDto disabled = new TmForgeModelDto
            {
                Elements = model.Elements,
                Flows = model.Flows,
                Analysis = new TmForgeAnalysisDto { DisabledPacks = new[] { "core-hygiene" } },
            };

            string enabled = Encoding.UTF8.GetString(EngineService.AnalysisReport(model, "json"));
            string suppressed = Encoding.UTF8.GetString(EngineService.AnalysisReport(disabled, "json"));

            Assert.IsTrue(CountResults(enabled) > CountResults(suppressed));
        }

        /// <summary>Writing to a caller's stream leaves it open and positioned for more content.</summary>
        [TestMethod]
        public void WriteAnalysisReportLeavesTheStreamOpen()
        {
            using MemoryStream stream = new MemoryStream();

            EngineService.WriteAnalysisReport(SampleModel(), "sarif", stream, null);

            Assert.IsTrue(stream.CanWrite, "the caller still owns the stream");
            Assert.IsTrue(stream.Length > 0);
            stream.WriteByte(32);
        }

        /// <summary>A null destination is rejected rather than silently discarding a report.</summary>
        [TestMethod]
        public void WriteAnalysisReportRejectsANullStream()
        {
            Assert.Throws<ArgumentNullException>(
                () => EngineService.WriteAnalysisReport(SampleModel(), "sarif", null!, null));
        }

        private static int CountResults(string reportJson)
        {
            using JsonDocument document = JsonDocument.Parse(reportJson);
            return document.RootElement.GetProperty("ruleReports")
                .EnumerateArray()
                .Sum(rule => rule.TryGetProperty("messages", out JsonElement messages) ? messages.GetArrayLength() : 0);
        }

        private static TmForgeModelDto SampleModel()
        {
            return new TmForgeModelDto
            {
                Elements = new[]
                {
                    new TmForgeElementDto { Id = "e1", Kind = "external", Name = "Customer" },
                    new TmForgeElementDto { Id = "p1", Kind = "process", Name = "Gateway" },
                    new TmForgeElementDto { Id = "s1", Kind = "datastore" },
                },
                Flows = new[]
                {
                    new TmForgeFlowDto { Id = "f1", Source = "e1", Target = "p1", Name = "request" },
                },
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
