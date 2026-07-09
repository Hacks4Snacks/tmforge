namespace ThreatModelForge.Analysis.Reporting.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.CodeAnalysis.Sarif;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    /// <summary>
    /// Unit tests for the <see cref="SarifReportWriter"/> class.
    /// </summary>
    [TestClass]
    public class SarifReportWriterTests
    {
        private const string PeriodString = ".";

        /// <summary>
        /// Gets or sets the test context.
        /// </summary>
        public TestContext? TestContext { get; set; }

        /// <summary>
        /// Unit test for <see cref="SarifReportWriter.Write"/> method.
        /// </summary>
        [TestMethod]
        public void WriteSingleRuleSingleMessage()
        {
            Assert.IsNotNull(this.TestContext!.DeploymentDirectory);
            var docFilePath = Path.Join(
                this.TestContext!.DeploymentDirectory,
                "Gateway.tm7");
            ModelReport report = CreateTestReport(docFilePath);
            CreateTestRules(report, 1, 1, MessageSeverity.Error);
            CreateTestMessages(report.RuleReports.First(), 1);

            RunTest(report, docFilePath);
        }

        /// <summary>
        /// Unit test for <see cref="SarifReportWriter.Write"/> method.
        /// </summary>
        [TestMethod]
        public void WriteNullModelReport()
        {
            Assert.IsNotNull(this.TestContext!.DeploymentDirectory);
            var docFilePath = Path.Join(
                this.TestContext!.DeploymentDirectory, "Partner.tm7");
            ModelReport? report = null;

            Assert.Throws<ArgumentNullException>(() => RunTest(report, docFilePath));
        }

        /// <summary>
        /// Unit test for <see cref="SarifReportWriter.Write"/> method.
        /// </summary>
        [TestMethod]
        public void WriteNoRule()
        {
            Assert.IsNotNull(this.TestContext!.DeploymentDirectory);
            var docFilePath = Path.Join(
                this.TestContext!.DeploymentDirectory, "Gateway.tm7");
            ModelReport report = CreateTestReport(docFilePath);

            RunTest(report, docFilePath);
        }

        /// <summary>
        /// Unit test for <see cref="SarifReportWriter.Write"/> method.
        /// </summary>
        [TestMethod]
        public void WriteSingleRuleMultipleResults()
        {
            Assert.IsNotNull(this.TestContext!.DeploymentDirectory);
            var docFilePath = Path.Join(
                this.TestContext!.DeploymentDirectory,
                "Subscriptions.tm7");
            ModelReport report = CreateTestReport(docFilePath);
            CreateTestRules(report, 1, 1, MessageSeverity.Warning);
            CreateTestMessages(report.RuleReports.First(), 8);

            RunTest(report, docFilePath);
        }

        /// <summary>
        /// Unit test for <see cref="SarifReportWriter.Write"/> method.
        /// </summary>
        [TestMethod]
        public void WriteSingleRuleZeroResults()
        {
            Assert.IsNotNull(this.TestContext!.DeploymentDirectory);
            var docFilePath = Path.Join(
                this.TestContext!.DeploymentDirectory,
                "Orders.tm7");
            ModelReport report = CreateTestReport(docFilePath);
            CreateTestRules(report, 1, 1, MessageSeverity.Warning);

            RunTest(report, docFilePath);
        }

        /// <summary>
        /// Unit test for <see cref="SarifReportWriter.Write"/> method.
        /// </summary>
        [TestMethod]
        public void WriteMultiRuleZeroResults()
        {
            Assert.IsNotNull(this.TestContext!.DeploymentDirectory);
            var docFilePath = Path.Join(
                this.TestContext!.DeploymentDirectory,
                "Support Service.tm7");
            ModelReport report = CreateTestReport(docFilePath);
            CreateTestRules(report, 1, 5, MessageSeverity.Error);
            CreateTestRules(report, 6, 3, MessageSeverity.Warning);
            CreateTestRules(report, 9, 3, MessageSeverity.Info);

            RunTest(report, docFilePath);
        }

        /// <summary>
        /// Unit test for <see cref="SarifReportWriter.Write"/> method.
        /// </summary>
        [TestMethod]
        public void WriteMultiRuleSingleResults()
        {
            Assert.IsNotNull(this.TestContext!.DeploymentDirectory);
            var docFilePath = Path.Join(
                this.TestContext!.DeploymentDirectory,
                "Tenant.Profile.tm7");
            ModelReport report = CreateTestReport(docFilePath);
            CreateTestRules(report, 1, 5, MessageSeverity.Error);
            CreateTestMessages(report.RuleReports.Last(), 1);
            CreateTestRules(report, 6, 3, MessageSeverity.Warning);
            CreateTestMessages(report.RuleReports.Last(), 1);
            CreateTestRules(report, 9, 3, MessageSeverity.Info);
            CreateTestMessages(report.RuleReports.Last(), 1);

            RunTest(report, docFilePath);
        }

        /// <summary>
        /// Unit test for <see cref="SarifReportWriter.Write"/> method.
        /// </summary>
        [TestMethod]
        public void WriteMultiRuleMultipleResults()
        {
            Assert.IsNotNull(this.TestContext!.DeploymentDirectory);
            var docFilePath = Path.Join(
                this.TestContext!.DeploymentDirectory,
                "Gateway.tm7");
            ModelReport report = CreateTestReport(docFilePath);
            CreateTestRules(report, 1, 5, MessageSeverity.Error);
            CreateTestMessages(report.RuleReports.TakeLast(3).First(), 10);
            CreateTestRules(report, 6, 3, MessageSeverity.Warning);
            CreateTestMessages(report.RuleReports.TakeLast(2).First(), 1);
            CreateTestRules(report, 9, 3, MessageSeverity.Info);
            CreateTestMessages(report.RuleReports.TakeLast(1).First(), 6);

            RunTest(report, docFilePath);
        }

        /// <summary>
        /// Unit test for <see cref="SarifReportWriter.Write"/> method.
        /// </summary>
        [TestMethod]
        public void WriteSingeRuleMultipleResultsSuppressed()
        {
            Assert.IsNotNull(this.TestContext!.DeploymentDirectory);
            var docFilePath = Path.Join(
                this.TestContext!.DeploymentDirectory,
                "Sync_Agent.tm7");
            ModelReport report = CreateTestReport(docFilePath);
            CreateTestRules(report, 1, 1, MessageSeverity.Error);
            CreateTestMessages(report.RuleReports.First(), 3, suppressed: true);

            RunTest(report, docFilePath);
        }

        /// <summary>
        /// Unit test for <see cref="SarifReportWriter.Write"/> method.
        /// </summary>
        [TestMethod]
        public void WriteSingeRuleMultipleResultsMixedSuppressed()
        {
            Assert.IsNotNull(this.TestContext!.DeploymentDirectory);
            var docFilePath = Path.Join(
                this.TestContext!.DeploymentDirectory,
                "Catalog-1.tm7");
            ModelReport report = CreateTestReport(docFilePath);
            CreateTestRules(report, 1, 1, MessageSeverity.Error);
            CreateTestMessages(report.RuleReports.First(), 4, suppressed: false);
            CreateTestMessages(report.RuleReports.First(), 2, suppressed: true);

            RunTest(report, docFilePath);
        }

        /// <summary>
        /// Unit test for <see cref="SarifReportWriter.Write"/> method.
        /// </summary>
        [TestMethod]
        public void WriteMultiRuleMultipleResultsMixedSuppressed()
        {
            Assert.IsNotNull(this.TestContext!.DeploymentDirectory);
            var docFilePath = Path.Join(
                this.TestContext!.DeploymentDirectory,
                "Journals.tm7");
            ModelReport report = CreateTestReport(docFilePath);
            CreateTestRules(report, 1, 7, MessageSeverity.Error);
            CreateTestMessages(report.RuleReports.TakeLast(5).First(), 4, suppressed: false);
            CreateTestMessages(report.RuleReports.TakeLast(5).First(), 3, suppressed: true);

            CreateTestRules(report, 8, 4, MessageSeverity.Warning);
            CreateTestMessages(report.RuleReports.TakeLast(2).First(), 2, suppressed: false);
            CreateTestMessages(report.RuleReports.TakeLast(3).First(), 1, suppressed: true);

            CreateTestRules(report, 12, 2, MessageSeverity.Info);
            CreateTestMessages(report.RuleReports.TakeLast(1).First(), 6, suppressed: true);

            RunTest(report, docFilePath);
        }

        private static void AssertSarifLog(
            SarifLog? sarifLog,
            ModelReport report,
            string documentPath)
        {
            Assert.IsNotNull(sarifLog);

            Run run = sarifLog!.Runs.First();
            Assert.IsNotNull(run);

            // verify tool information
            Assert.IsNotNull(report.AnalysisTool);
            Assert.AreEqual(run.Tool.Driver.Name, report.AnalysisTool!.Name);
            Assert.AreEqual(run.Tool.Driver.FullName, report.AnalysisTool!.FullName);
            Assert.AreEqual(run.Tool.Driver.Version, report.AnalysisTool!.Version);
            Assert.AreEqual(run.Tool.Driver.Organization, report.AnalysisTool!.Organization);
            Assert.AreEqual(run.Tool.Driver.InformationUri, report.AnalysisTool!.InformationUri);

            // verify SARIF rules and results
            IList<ReportingDescriptor> rules = run.Tool.Driver.Rules;
            int ruleCount = report.RuleReports.Count(r => r.Messages.Any() || r.SuppressedMessages.Any());
            if (ruleCount == 0)
            {
                Assert.IsNull(rules);
            }
            else
            {
                Assert.AreEqual(rules.Count, ruleCount);
                foreach (var sarifRule in rules)
                {
                    var rule = report.RuleReports.First(r => r.ID == sarifRule.Id);
                    Assert.AreEqual(sarifRule.Id, rule.ID);
                    Assert.AreEqual(sarifRule.Name, $"{rule.AnalyzerId!.Substring(0, rule.AnalyzerId.IndexOf(','))}/{rule.Name}");
                    Assert.AreEqual(sarifRule.FullDescription.Text, rule.FullDescription!.EndsWith(PeriodString, StringComparison.OrdinalIgnoreCase) ? rule.FullDescription : rule.FullDescription + PeriodString);
                    Assert.AreEqual(sarifRule.Help.Text, rule.HelpText!.EndsWith(PeriodString, StringComparison.OrdinalIgnoreCase) ? rule.HelpText : rule.HelpText + PeriodString);
                    if (rule.HelpUri != null)
                    {
                        Assert.AreEqual(sarifRule.HelpUri, rule.HelpUri);
                    }

                    var sarifResults = run.Results.Where(r => r.RuleId == sarifRule.Id);
                    Assert.AreEqual(sarifResults.Count(), rule.Messages.Count + rule.SuppressedMessages.Count);
                    foreach (var sarifResult in sarifResults)
                    {
                        Assert.AreEqual(sarifResult.RuleId, rule.ID);
                        Assert.AreEqual(sarifResult.Level, GetSarifLevel(rule.Severity));
                        Assert.AreEqual(sarifResult.Locations.First().PhysicalLocation.ArtifactLocation.Uri.OriginalString, Path.GetFileName(documentPath));
                        if (sarifResult.TryIsSuppressed(out bool isSuppressed) && isSuppressed)
                        {
                            Assert.IsTrue(rule.SuppressedMessages.Any(m => m.Text!.Equals(sarifResult.Message.Text)));
                        }
                        else
                        {
                            Assert.IsTrue(rule.Messages.Any(m => m.Text!.Equals(sarifResult.Message.Text)));
                        }
                    }
                }
            }

            Assert.HasCount(1, run.OriginalUriBaseIds);
            Assert.AreEqual(
                run.OriginalUriBaseIds["ROOTPATH"].Uri,
                new Uri(Path.GetDirectoryName(documentPath) ?? string.Empty, UriKind.Absolute));

            // verify all rule assemblies are logged in SARIF invocation
            Invocation invocation = run.Invocations.First();
            foreach (var rule in report.RuleReports)
            {
                Assert.AreEqual(invocation.GetProperty(rule.ID), rule.AnalyzerId);
            }
        }

        private static ModelReport CreateTestReport(string documentPath)
        {
            var modelReport = new ModelReport
            {
                SourcePath = documentPath,
                ThreatModelName = "Gateway",
                ToolVersion = "4.3",
                KnowledgeBaseName = "Azure Threat Model Template",
                KnowledgeBaseVersion = "1.0.0.33",
                AnalysisTool = new ToolInfo
                {
                    Name = "tm7-lint",
                    FullName = "Threat Model Forge Lint",
                    Organization = "Threat Model Forge",
                    Version = "1.0.0.0",
                    InformationUri = new Uri("https://example.com/tmforge"),
                },
            };
            return modelReport;
        }

        private static void CreateTestRules(ModelReport report, int start, int ruleCount, MessageSeverity severity)
        {
            for (int i = start; i < start + ruleCount; i++)
            {
                string ruleId = $"{i:00000}";
                report.RuleReports.Add(new RuleReport
                {
                    ID = $"TEST{ruleId}",
                    Name = $"TmtLintTestRule{ruleId}",
                    FullDescription = $"Full description for test rule {ruleId}", // not end with period
                    HelpText = $"Recommendation to resolve the issue {ruleId}.", // end with period
                    HelpUri = new Uri($"http://testrule.tmtlinter.com/{ruleId}"),
                    AnalyzerId = "Analysis.TestRuleAssembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                    Severity = severity,
                });
            }
        }

        private static void CreateTestMessages(RuleReport rule, int messageCount, bool suppressed = false)
        {
            for (int i = 0; i < messageCount; i++)
            {
                var message = new RuleReportMessage
                {
                    Text = $"Entity {Guid.NewGuid()} has voilated rule {rule.ID}",
                };
                if (suppressed)
                {
                    rule.SuppressedMessages.Add(message);
                }
                else
                {
                    rule.Messages.Add(message);
                }
            }
        }

        private static FailureLevel GetSarifLevel(MessageSeverity severity)
        {
            return severity switch
            {
                MessageSeverity.Error => FailureLevel.Error,
                MessageSeverity.Warning => FailureLevel.Warning,
                MessageSeverity.Info => FailureLevel.Note,
                _ => throw new ArgumentException($"{nameof(severity)} value is not supported."),
            };
        }

        private static void RunTest(ModelReport? testcase, string docFilePath)
        {
            using var memStream = new MemoryStream();
            using var writer = new SarifReportWriter(memStream, docFilePath);
            writer.Write(testcase!);

            // assert
            SarifLog? sarifLog = JsonConvert.DeserializeObject<SarifLog>(
                ASCIIEncoding.UTF8.GetString(memStream.ToArray()));
            AssertSarifLog(sarifLog, testcase!, docFilePath);
        }
    }
}
