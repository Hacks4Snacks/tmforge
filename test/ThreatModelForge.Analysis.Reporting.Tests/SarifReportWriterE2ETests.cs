namespace ThreatModelForge.Analysis.Reporting.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using Microsoft.CodeAnalysis.Sarif;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using ThreatModelForge.Model;

    /// <summary>
    /// E2E tests for the <see cref="SarifReportWriter"/> class.
    /// </summary>
    [TestClass]
    [DeploymentItem("GatewayModel.tm7")]
    public class SarifReportWriterE2ETests
    {
        private const string PeriodString = ".";

        /// <summary>
        /// Gets or sets the test context.
        /// </summary>
        public TestContext? TestContext { get; set; }

        /// <summary>
        /// Test for <see cref="SarifReportWriter"/> generates SARIF report from results of analyzing a test threat modeling document.
        /// </summary>
        [TestMethod]
        public void AnalyzeThreatModelingDocumentTest()
        {
            const string docFileName = "GatewayModel.tm7";
            Assert.IsNotNull(this.TestContext!.DeploymentDirectory);
            string docFilePath = Path.Combine(
                this.TestContext!.DeploymentDirectory,
                docFileName);

            using RuleSet ruleSet = RuleSet.LoadDefault(new[]
            {
                Assembly.Load("ThreatModelForge.Analysis.Rules"),
            });

            ThreatModel model = ThreatModel.Load(docFilePath);
            ToolInfo tool = LoadToolInfo();
            TestMessageWriter writer = new (docFilePath);
            RuleEvaluationContext context = new (
                model,
                writer,
                null,
                docFilePath,
                tool);

            ruleSet.Evaluate(context);
            ModelReport report = context.GenerateReport(ruleSet);
            var memStream = new MemoryStream();
            using var sarifWriter = new SarifReportWriter(memStream, docFilePath);
            sarifWriter.Write(report);

            // assert
            const string ruleId = "TM1002";
            string artifactUriString = $"{docFileName}";

            SarifLog? sarifLog = JsonConvert.DeserializeObject<SarifLog>(
                ASCIIEncoding.UTF8.GetString(memStream.ToArray()));
            Assert.IsNotNull(sarifLog);

            Run run = sarifLog!.Runs.First();
            ToolComponent driver = run.Tool.Driver;
            Assert.AreEqual(tool.Name, driver.Name);
            Assert.AreEqual(tool.FullName, driver.FullName);
            Assert.AreEqual(tool.Version, driver.Version);
            Assert.AreEqual(tool.Organization, driver.Organization);
            Assert.AreEqual(tool.InformationUri, driver.InformationUri);

            // analysis result of test tm7 file has 12 warning results of rule TM1002
            ReportingDescriptor? sarifRule =
                run.Tool.Driver.Rules
                .Where(e => string.Equals(e.Id, ruleId, StringComparison.Ordinal))
                .FirstOrDefault();
            Assert.IsNotNull(sarifRule);
            Assert.AreEqual(
                "ThreatModelForge.Analysis.Rules/DescriptiveEdgeNameRule",
                sarifRule!.Name);
            Assert.AreNotEqual(PeriodString, sarifRule.FullDescription.Text);
            Assert.AreNotEqual(PeriodString, sarifRule.Help.Text);
            Assert.IsNotNull(sarifRule.HelpUri);

            Assert.AreEqual(1, run.OriginalUriBaseIds.Count);
            Assert.AreEqual(
                new Uri(this.TestContext.DeploymentDirectory, UriKind.Absolute),
                run.OriginalUriBaseIds["ROOTPATH"].Uri);
            IEnumerable<Result> runResults =
                run.Results
                .Where(e => string.Equals(e.RuleId, ruleId, StringComparison.Ordinal));
            Assert.AreEqual(12, runResults.Count());
            foreach (Result result in runResults)
            {
                Assert.AreEqual(ruleId, result.RuleId);
                Assert.AreEqual(FailureLevel.Warning, result.Level);
                Assert.AreEqual(artifactUriString, result.Locations.First().PhysicalLocation.ArtifactLocation.Uri.OriginalString);
            }

            Invocation? invocation = run.Invocations.FirstOrDefault();
            Assert.IsNotNull(invocation);
            foreach (var rule in report.RuleReports)
            {
                Assert.AreEqual(
                    rule.AnalyzerId,
                    invocation!.GetProperty(rule.ID));
            }
        }

        private static ToolInfo LoadToolInfo()
        {
            return new ToolInfo
            {
                Name = "tm7-lint",
                FullName = "Threat Model Forge Lint",
                Version = "1.0.0.0",
                Organization = "Threat Model Forge",
                InformationUri = new Uri("https://example.com/tmforge"),
            };
        }
    }
}
