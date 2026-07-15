namespace ThreatModelForge.Analysis.Reporting
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.CodeAnalysis.Sarif;
    using Microsoft.CodeAnalysis.Sarif.Writers;

    /// <summary>
    /// Class to generate SARIF report from ModelReport data.
    /// </summary>
    public class SarifReportWriter : ReportWriter
    {
        private const string UriBaseIdString = "ROOTPATH";

        private const string PeriodString = ".";

        private readonly IDictionary<string, ReportingDescriptor> rulesDictionary;

        private Run? sarifRun;

        /// <summary>
        /// Initializes a new instance of the <see cref="SarifReportWriter"/> class.
        /// </summary>
        /// <param name="reportFilePath">The output SARIF report file path.</param>
        public SarifReportWriter(string reportFilePath)
            : this(CreateStream(reportFilePath), reportFilePath)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SarifReportWriter"/> class.
        /// </summary>
        /// <param name="stream">The stream to be used to create SARIF output.</param>
        /// <param name="reportFilePath">The path of SARIF report file to be created.</param>
        public SarifReportWriter(Stream stream, string reportFilePath)
        {
            this.ReportFilePath = !string.IsNullOrWhiteSpace(reportFilePath) ?
                                  reportFilePath :
                                  throw new ArgumentOutOfRangeException(nameof(reportFilePath));

            this.FileStream = stream ?? throw new ArgumentNullException(nameof(stream));
            this.OutputTextWriter = new StreamWriter(this.FileStream);
            this.rulesDictionary = new ConcurrentDictionary<string, ReportingDescriptor>();
        }

        /// <summary>
        /// Gets the path to the SARIIF report.
        /// </summary>
        public string ReportFilePath { get; }

        /// <summary>
        /// Gets or sets the threat modeling document file path.
        /// </summary>
        public string? DocumentFilePath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the analyzer outputs have suppressed messages.
        /// </summary>
        public bool HasSuppressedMessage { get; set; }

        /// <summary>
        /// Gets the stream object.
        /// </summary>
        public Stream FileStream { get; }

        /// <summary>
        /// Gets the stream writer object.
        /// </summary>
        public StreamWriter OutputTextWriter { get; }

        /// <summary>
        /// Write the reports to SARIF file.
        /// </summary>
        /// <param name="report">The report to write.</param>
        public override void Write(ModelReport report)
        {
            ModelReport modelReport = report ?? throw new ArgumentNullException(nameof(report));
            this.InitRun(modelReport);

            using (var sarifLogger = new SarifLogger(
                textWriter: this.OutputTextWriter,
                logFilePersistenceOptions: FilePersistenceOptions.PrettyPrint | FilePersistenceOptions.ForceOverwrite,
                run: this.sarifRun,
                levels: new FailureLevelSet(new[] { FailureLevel.Warning, FailureLevel.Error, FailureLevel.Note }),
                kinds: new ResultKindSet(new[] { ResultKind.Fail })))
            {
                foreach (var ruleReport in modelReport.RuleReports.Where(r => r.Messages.Any() || r.SuppressedMessages.Any()))
                {
                    _ = this.ExtractRule(ruleReport);
                    this.PersistResults(sarifLogger, this.ExtractResults(ruleReport));
                }

                this.PopulateRuleConfigurations(modelReport.RuleReports);
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.OutputTextWriter?.Dispose();
                this.FileStream?.Dispose();
            }

            base.Dispose(disposing);
        }

        private static Stream CreateStream(string filePath) =>
            new FileStream(filePath, FileMode.Create, FileAccess.Write);

        private static string AppendPeriod(string text) =>
            text.EndsWith(PeriodString, StringComparison.OrdinalIgnoreCase) ? text : text + PeriodString;

        private static string GetRuleName(RuleReport rule) =>
            $"{rule.AnalyzerId?.Substring(0, rule.AnalyzerId.IndexOf(','))}/{rule.Name}";

        private static FailureLevel GetSarifLevel(MessageSeverity severity)
        {
            switch (severity)
            {
                case MessageSeverity.Error:
                    return FailureLevel.Error;
                case MessageSeverity.Warning:
                    return FailureLevel.Warning;
                case MessageSeverity.Info:
                    return FailureLevel.Note;
                default:
                    throw new ArgumentException($"{nameof(severity)} value is not supported.");
            }
        }

        private void InitRun(ModelReport report)
        {
            this.sarifRun = new Run
            {
                Tool = new Tool
                {
                    Driver = new ToolComponent
                    {
                        Name = report.AnalysisTool?.Name,
                        FullName = report.AnalysisTool?.FullName,
                        Version = report.AnalysisTool?.Version,
                        InformationUri = report.AnalysisTool?.InformationUri,
                        Organization = report.AnalysisTool?.Organization,
                    },
                },
                OriginalUriBaseIds = new Dictionary<string, ArtifactLocation>()
                {
                    {
                        UriBaseIdString,
                        new ArtifactLocation
                        {
                            Uri = new Uri(
                                UriHelper.MakeValidUri(Path.GetDirectoryName(report.SourcePath)),
                                UriKind.RelativeOrAbsolute),
                        }
                    },
                },
            };

            if (report.ThreatCategories.Count > 0)
            {
                List<Dictionary<string, string>> categories = report.ThreatCategories
                    .OrderBy(category => category.Id, StringComparer.Ordinal)
                    .Select(category => new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["id"] = category.Id,
                        ["name"] = category.Name,
                        ["shortDescription"] = category.ShortDescription ?? string.Empty,
                        ["longDescription"] = category.LongDescription ?? string.Empty,
                    })
                    .ToList();
                this.sarifRun.Tool.Driver.SetProperty("threatCategories", categories);
            }

            this.DocumentFilePath = report.SourcePath;
            this.HasSuppressedMessage = report.RuleReports.Any(r => r.SuppressedMessages?.Any() == true);
        }

        private ReportingDescriptor ExtractRule(RuleReport rule)
        {
            if (!this.rulesDictionary.TryGetValue(rule.ID ?? string.Empty, out ReportingDescriptor sarifRule))
            {
                sarifRule = new ReportingDescriptor
                {
                    Id = rule.ID,
                    Name = GetRuleName(rule),
                    FullDescription = new MultiformatMessageString { Text = AppendPeriod(rule.FullDescription ?? string.Empty) },
                    Help = new MultiformatMessageString { Text = AppendPeriod(rule.HelpText ?? string.Empty) },
                    HelpUri = rule.HelpUri,
                    DefaultConfiguration = new ReportingConfiguration { Level = GetSarifLevel(rule.Severity) },
                };
                if (!string.IsNullOrWhiteSpace(rule.ThreatCategoryId))
                {
                    sarifRule.SetProperty("threatCategoryId", rule.ThreatCategoryId);
                    sarifRule.SetProperty("threatCategory", rule.ThreatCategoryName);
                }

                if (rule.Stride.HasValue)
                {
                    sarifRule.SetProperty("stride", rule.Stride.Value.ToString());
                }

                if (rule.DefaultThreatPriority.HasValue)
                {
                    sarifRule.SetProperty("defaultThreatPriority", rule.DefaultThreatPriority.Value.ToString());
                }

                this.rulesDictionary.Add(rule.ID ?? string.Empty, sarifRule);
            }

            return sarifRule;
        }

        private IEnumerable<Result> ExtractResults(RuleReport ruleReport)
        {
            foreach (var message in ruleReport.Messages)
            {
                yield return this.ExtractResult(ruleReport, message, suppressed: false);
            }

            foreach (var message in ruleReport.SuppressedMessages)
            {
                yield return this.ExtractResult(ruleReport, message, suppressed: true);
            }
        }

        private Result ExtractResult(RuleReport ruleReport, RuleReportMessage message, bool suppressed)
        {
            return new Result
            {
                RuleId = ruleReport.ID,
                Level = GetSarifLevel(ruleReport.Severity),
                Message = new Microsoft.CodeAnalysis.Sarif.Message { Text = message.Text },
                Locations = new[]
                {
                    new Location
                    {
                        PhysicalLocation = new PhysicalLocation
                        {
                            ArtifactLocation = new ArtifactLocation
                            {
                                Uri = new Uri(
                                    UriHelper.MakeValidUri(Path.GetFileName(this.DocumentFilePath)),
                                    UriKind.Relative),
                                UriBaseId = UriBaseIdString,
                            },
                        },
                    },
                },
                Suppressions = this.HasSuppressedMessage ?
                               (suppressed ?
                                    new[] { new Suppression { Status = SuppressionStatus.Accepted } } :
                                    Array.Empty<Suppression>()) :
                               null,
            };
        }

        private void PersistResults(SarifLogger logger, IEnumerable<Result> sarifResults)
        {
            if (sarifResults?.Any() == true)
            {
                foreach (var result in sarifResults)
                {
                    logger.Log(this.rulesDictionary[result.RuleId], result, extensionIndex: null);
                }
            }
        }

        private void PopulateRuleConfigurations(IEnumerable<RuleReport> rules)
        {
            if (this.sarifRun?.Invocations?.Any() == true)
            {
                Invocation invocation = this.sarifRun.Invocations.First();
                foreach (var rule in rules)
                {
                    invocation.SetProperty(rule.ID, rule.AnalyzerId);
                }
            }
        }
    }
}
