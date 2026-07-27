namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using ThreatModelForge.Engine;
    using ThreatModelForge.Model;

    /// <summary>
    /// Implements <c>tmforge analysis</c>: checks a stored <c>tmforge-analysis</c> document, and
    /// compares two of them.
    /// </summary>
    /// <remarks>
    /// A stored analysis is evidence, and evidence gets read long after it was written — by a gate, an
    /// auditor, or a person deciding whether a review still holds. Two things can be wrong with it and
    /// both are silent: the document can contradict itself, and it can be perfectly coherent but stale,
    /// describing a model that has since changed. <c>validate</c> checks for both, because a clean
    /// report about last month's architecture is the more dangerous of the two. <c>diff</c> answers the
    /// other question the artifact exists for: not how many findings there are now, but which ones
    /// arrived.
    /// </remarks>
    internal static class AnalysisCommand
    {
        private const int SuccessExitCode = 0;

        private const int ErrorExitCode = 1;

        private const int ProblemsExitCode = 2;

        /// <summary>Runs the analysis command.</summary>
        /// <param name="args">The command arguments (after the verb).</param>
        /// <returns>Zero when the document is sound; 1 on tool error; 2 when problems were found.</returns>
        public static int Run(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                PrintUsage();
                return ErrorExitCode;
            }

            string subcommand = args[0];
            if (string.Equals(subcommand, "-h", StringComparison.Ordinal) ||
                string.Equals(subcommand, "--help", StringComparison.Ordinal) ||
                string.Equals(subcommand, "-?", StringComparison.Ordinal))
            {
                PrintUsage();
                return SuccessExitCode;
            }

            if (string.Equals(subcommand, "diff", StringComparison.Ordinal))
            {
                return Diff(args[1..]);
            }

            if (!string.Equals(subcommand, "validate", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("Unknown subcommand: " + subcommand);
                PrintUsage();
                return ErrorExitCode;
            }

            return Validate(args[1..]);
        }

        private static int Validate(string[] args)
        {
            CliArgs parsed = CliArgs.Parse(args, new[] { "model", "expect-version" }, Array.Empty<string>());
            if (parsed.Help)
            {
                PrintUsage();
                return SuccessExitCode;
            }

            if (parsed.UnknownFlags.Count > 0)
            {
                Console.Error.WriteLine("Unknown option: " + parsed.UnknownFlags[0]);
                PrintUsage();
                return ErrorExitCode;
            }

            string? input = parsed.Positionals.Count > 0 ? parsed.Positionals[0] : null;
            if (string.IsNullOrEmpty(input))
            {
                PrintUsage();
                return ErrorExitCode;
            }

            if (!File.Exists(input))
            {
                Console.Error.WriteLine("File not found: " + input);
                return ErrorExitCode;
            }

            int? expected = null;
            string? expectedText = parsed.Get("expect-version");
            if (!string.IsNullOrWhiteSpace(expectedText))
            {
                if (!int.TryParse(expectedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    Console.Error.WriteLine("--expect-version must be a whole number.");
                    return ErrorExitCode;
                }

                expected = value;
            }

            if (!AnalysisDocumentReader.TryRead(
                File.ReadAllText(input!),
                expected,
                out AnalysisDocumentDto? document,
                out IReadOnlyList<string> readProblems))
            {
                return Report(parsed.Json, input!, readProblems, stale: false);
            }

            List<string> problems = new List<string>(AnalysisDocumentValidator.Validate(document!));
            bool stale = false;

            string? modelPath = parsed.Get("model");
            if (!string.IsNullOrWhiteSpace(modelPath))
            {
                if (!File.Exists(modelPath))
                {
                    Console.Error.WriteLine("File not found: " + modelPath);
                    return ErrorExitCode;
                }

                (ThreatModel model, _) = CliModelLoader.Load(modelPath!);
                string actual = AnalysisDocumentBuilder.ModelFingerprint(model);
                if (!string.Equals(actual, document!.Model.Fingerprint, StringComparison.Ordinal))
                {
                    stale = true;
                    problems.Add(
                        $"The document describes a different model: it records {document.Model.Fingerprint} " +
                        $"but '{Path.GetFileName(modelPath)}' is {actual}. Re-run the analysis.");
                }
            }

            return Report(parsed.Json, input!, problems, stale, document);
        }

        /// <summary>
        /// Compares two stored analyses by finding identity, so the answer is which findings arrived
        /// and which went away rather than how many there are.
        /// </summary>
        /// <param name="args">The arguments after the subcommand.</param>
        /// <returns>Zero when nothing was introduced; 1 on tool error; 2 when findings were introduced.</returns>
        private static int Diff(string[] args)
        {
            CliArgs parsed = CliArgs.Parse(args, Array.Empty<string>(), Array.Empty<string>());
            if (parsed.Help)
            {
                PrintUsage();
                return SuccessExitCode;
            }

            if (parsed.UnknownFlags.Count > 0)
            {
                Console.Error.WriteLine("Unknown option: " + parsed.UnknownFlags[0]);
                PrintUsage();
                return ErrorExitCode;
            }

            if (parsed.Positionals.Count < 2)
            {
                PrintUsage();
                return ErrorExitCode;
            }

            if (!TryLoad(parsed.Positionals[0], out AnalysisDocumentDto? baseDocument) ||
                !TryLoad(parsed.Positionals[1], out AnalysisDocumentDto? headDocument))
            {
                return ErrorExitCode;
            }

            AnalysisDifference difference = AnalysisDocumentDiff.Compare(baseDocument!, headDocument!);

            if (parsed.Json)
            {
                CliJson.WriteEnvelope("analysis", new
                {
                    operation = "diff",
                    baseDocument = parsed.Positionals[0],
                    headDocument = parsed.Positionals[1],
                    summary = new
                    {
                        introduced = difference.Introduced.Count,
                        resolved = difference.Resolved.Count,
                        reclassified = difference.Reclassified.Count,
                        unchanged = difference.Unchanged,
                    },
                    introduced = difference.Introduced.Select(Describe).ToArray(),
                    resolved = difference.Resolved.Select(Describe).ToArray(),
                    reclassified = difference.Reclassified
                        .Select(change => new
                        {
                            id = change.After.Id,
                            ruleId = change.After.RuleId,
                            diagram = change.After.Diagram,
                            message = change.After.Message,
                            from = new { disposition = change.Before.Disposition, severity = change.Before.Severity },
                            to = new { disposition = change.After.Disposition, severity = change.After.Severity },
                        })
                        .ToArray(),
                    warnings = difference.Warnings,
                });
            }
            else
            {
                WriteDifference(difference);
            }

            // The warnings go to stderr in both modes: they qualify the answer rather than form part
            // of it, and a caller piping --json into a tool should still see them.
            foreach (string warning in difference.Warnings)
            {
                Console.Error.WriteLine("warning: " + warning);
            }

            return difference.Introduced.Count > 0 ? ProblemsExitCode : SuccessExitCode;
        }

        private static bool TryLoad(string path, out AnalysisDocumentDto? document)
        {
            document = null;
            if (!File.Exists(path))
            {
                Console.Error.WriteLine("File not found: " + path);
                return false;
            }

            if (AnalysisDocumentReader.TryRead(
                File.ReadAllText(path),
                null,
                out document,
                out IReadOnlyList<string> problems))
            {
                return true;
            }

            foreach (string problem in problems)
            {
                Console.Error.WriteLine(path + ": " + problem);
            }

            return false;
        }

        private static void WriteDifference(AnalysisDifference difference)
        {
            if (difference.IsEmpty)
            {
                Console.WriteLine(
                    "No change in findings (" +
                    difference.Unchanged.ToString(CultureInfo.InvariantCulture) + " unchanged).");
                return;
            }

            WriteFindings("Introduced", difference.Introduced);
            WriteFindings("Resolved", difference.Resolved);

            if (difference.Reclassified.Count > 0)
            {
                Console.WriteLine("Reclassified:");
                foreach (AnalysisFindingChange change in difference.Reclassified)
                {
                    Console.WriteLine(
                        "  " + change.After.Id + "  " +
                        Classification(change.Before) + " -> " + Classification(change.After));
                    Console.WriteLine("      " + change.After.Message);
                }

                Console.WriteLine();
            }

            Console.WriteLine(
                difference.Introduced.Count.ToString(CultureInfo.InvariantCulture) + " introduced, "
                + difference.Resolved.Count.ToString(CultureInfo.InvariantCulture) + " resolved, "
                + difference.Reclassified.Count.ToString(CultureInfo.InvariantCulture) + " reclassified, "
                + difference.Unchanged.ToString(CultureInfo.InvariantCulture) + " unchanged.");
        }

        private static void WriteFindings(string title, IReadOnlyList<AnalysisFindingDto> findings)
        {
            if (findings.Count == 0)
            {
                return;
            }

            Console.WriteLine(title + ":");
            foreach (AnalysisFindingDto finding in findings)
            {
                Console.WriteLine("  " + finding.Severity + "  " + finding.Id + "  (" + finding.Disposition + ")");
                Console.WriteLine("      " + finding.Message);
            }

            Console.WriteLine();
        }

        private static string Classification(AnalysisFindingDto finding)
        {
            return finding.Severity + "/" + finding.Disposition;
        }

        private static object Describe(AnalysisFindingDto finding)
        {
            return new
            {
                id = finding.Id,
                ruleId = finding.RuleId,
                severity = finding.Severity,
                disposition = finding.Disposition,
                diagram = finding.Diagram,
                message = finding.Message,
                threatId = finding.ThreatId,
            };
        }

        private static int Report(
            bool json,
            string path,
            IReadOnlyList<string> problems,
            bool stale,
            AnalysisDocumentDto? document = null)
        {
            if (json)
            {
                CliJson.WriteEnvelope("analysis", new
                {
                    operation = "validate",
                    path,
                    status = problems.Count == 0 ? "valid" : "invalid",
                    stale,
                    schemaVersion = document?.Version,
                    findingCount = document?.Findings.Count,
                    problems,
                });
            }
            else if (problems.Count == 0)
            {
                int count = document?.Findings.Count ?? 0;
                Console.WriteLine(
                    $"{path}: valid tmforge-analysis v{document?.Version.ToString(CultureInfo.InvariantCulture)} " +
                    $"({count.ToString(CultureInfo.InvariantCulture)} findings).");
            }
            else
            {
                foreach (string problem in problems)
                {
                    Console.Error.WriteLine(path + ": " + problem);
                }
            }

            return problems.Count == 0 ? SuccessExitCode : ProblemsExitCode;
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Validate a stored tmforge-analysis document, or compare two of them.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge analysis validate [--model <path>] [--expect-version <n>] [--json] <document>");
            Console.Error.WriteLine("  tmforge analysis diff [--json] <base> <head>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  --model <path>          Also check the document against the model it describes, so a");
            Console.Error.WriteLine("                          coherent but stale analysis is reported instead of trusted.");
            Console.Error.WriteLine("  --expect-version <n>    Pin the schema version this caller was written against; any");
            Console.Error.WriteLine("                          other version is refused rather than read on wrong assumptions.");
            Console.Error.WriteLine("  --json                  Emit the result as JSON.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("diff matches findings on their stable id, so it reports which findings were introduced");
            Console.Error.WriteLine("and resolved rather than how the count moved, and renaming an element is not a change.");
            Console.Error.WriteLine("A changed rule selection is reported as a warning, not silently folded in.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Exit codes: 0 = sound (diff: nothing introduced); 1 = tool error;");
            Console.Error.WriteLine("            2 = problems found (diff: findings introduced).");
        }
    }
}
