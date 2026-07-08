namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Reflection;
    using System.Text.Json;
    using ThreatModelForge.Analysis;
    using ThreatModelForge.Analysis.Reporting;
    using ThreatModelForge.Formats;
    using ThreatModelForge.Model;

    /// <summary>
    /// Implements the <c>tmforge analyze</c> command.
    /// </summary>
    internal static class AnalyzeCommand
    {
        private const int SuccessExitCode = 0;

        private const int ErrorExitCode = 1;

        private const int FindingsExitCode = 2;

        /// <summary>
        /// Runs the analyze command.
        /// </summary>
        /// <param name="args">The command arguments (after the verb).</param>
        /// <returns>Program exit code.</returns>
        public static int Run(string[] args)
        {
            try
            {
                if (args == null)
                {
                    throw new ArgumentNullException(nameof(args));
                }

                if (!AnalyzeArguments.TryParse(args, out AnalyzeArguments? arguments))
                {
                    PrintUsage();
                    return ErrorExitCode;
                }

                using (RuleSet ruleSet = GetRuleSet(arguments!.RuleSetPath, arguments.RulePath))
                {
                    if (string.IsNullOrWhiteSpace(arguments.RuleSetPath))
                    {
                        ApplyModelRuleSelection(ruleSet, arguments.Path);
                    }

                    ThreatModel model = ThreatModelFormatRegistry.CreateDefault().Load(arguments.Path);
                    ConsoleMessageWriter writer = new ConsoleMessageWriter(arguments.Path, arguments.Json);
                    RuleEvaluationContext context = new RuleEvaluationContext(
                        model,
                        writer,
                        new ReadOnlyDictionary<string, string>(arguments.Variables),
                        arguments.Path,
                        LoadToolInfo());

                    if (!string.IsNullOrWhiteSpace(arguments.SuppressionFilePath))
                    {
                        SuppressionDocument suppressionDoc = SuppressionDocument.Load(arguments.SuppressionFilePath);
                        context.ApplySuppressions(
                            suppressionDoc.GetSuppressions(arguments.Path),
                            ruleSet);
                    }

                    ruleSet.Evaluate(context);

                    if (!string.IsNullOrWhiteSpace(arguments.ReportFolderPath))
                    {
                        ModelReport report = context.GenerateReport(ruleSet);
                        ModelListing listing = context.GenerateListing();
                        WriteReports(
                            report,
                            listing,
                            arguments.ReportFolderPath);
                    }

                    if (arguments.Json)
                    {
                        CliJson.WriteEnvelope("analyze", context.GenerateReport(ruleSet));
                    }

                    if (writer.MeetsThreshold(arguments.MaxSeverity))
                    {
                        return FindingsExitCode;
                    }
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Console.Error.WriteLine(ex.ToString());
                return ErrorExitCode;
            }

            return SuccessExitCode;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine(Properties.Resources.Usage);
        }

        private static void ApplyModelRuleSelection(RuleSet ruleSet, string path)
        {
            // The analysis selection travels only in the native tmforge-json format; other formats
            // (for example .tm7) fall back to the full rule set or an explicit --ruleset override.
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                if (!new TmForgeJsonFormat().CanRead(stream))
                {
                    return;
                }

                if (TmForgeJsonFormat.TryReadAnalysis(
                    stream,
                    out IReadOnlyList<string> disabledPacks,
                    out IReadOnlyList<string> disabledRuleIds))
                {
                    ruleSet.Disable(disabledPacks, disabledRuleIds);
                }
            }
        }

        private static void WriteReports(
            ModelReport report,
            ModelListing listing,
            string reportFolderPath)
        {
            if (!Directory.Exists(reportFolderPath))
            {
                Directory.CreateDirectory(reportFolderPath);
            }

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                Converters =
                {
                    new System.Text.Json.Serialization.JsonStringEnumConverter(),
                },
            };

            string? targetFileName = Path.GetFileNameWithoutExtension(report.SourcePath);
            string jsonPath = Path.Combine(
                reportFolderPath,
                $"{targetFileName!}.json");
            string jsonString = JsonSerializer.Serialize(report, options);
            File.WriteAllText(jsonPath, jsonString);

            string listingPath = Path.Combine(
                reportFolderPath,
                $"{targetFileName!}.listing.json");
            string listingJsonString = JsonSerializer.Serialize(listing, options);
            File.WriteAllText(listingPath, listingJsonString);

            string htmlPath = Path.Combine(
                reportFolderPath,
                $"{targetFileName!}.html");
            using (FindingsHtmlReportWriter writer = new FindingsHtmlReportWriter(htmlPath))
            {
                writer.Write(report);
            }

            string sarifFilePath = Path.Combine(
                reportFolderPath,
                $"{targetFileName!}.sarif");
            using (SarifReportWriter writer = new SarifReportWriter(sarifFilePath))
            {
                writer.Write(report);
            }
        }

        private static RuleSet GetRuleSet(string ruleSetPath, string rulePath)
        {
            RuleSet ruleSet = AnalysisRuleSources.Create(RuleSourceCli.FromPath(rulePath));
            if (!string.IsNullOrWhiteSpace(ruleSetPath))
            {
                ruleSet.ApplyOverrides(ruleSetPath);
            }

            return ruleSet;
        }

        private static ToolInfo LoadToolInfo()
        {
            return new ToolInfo
            {
                Name = Properties.Resources.ToolName,
                FullName = Properties.Resources.ToolFullName,
                Version = GetAssemblyFileVersion(),
                Organization = Properties.Resources.ToolOrganization,
                InformationUri = new Uri(Properties.Resources.ToolInformationUri),
            };
        }

        private static string GetAssemblyFileVersion()
        {
            return
                typeof(Program)
                .Assembly
                .GetCustomAttribute<AssemblyFileVersionAttribute>()?
                .Version ?? string.Empty;
        }
    }
}
