namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using ThreatModelForge.Engine;
    using ThreatModelForge.Model;

    /// <summary>
    /// Implements <c>tmforge analysis validate</c>: checks a stored <c>tmforge-analysis</c> document,
    /// and optionally checks it against the model it claims to describe.
    /// </summary>
    /// <remarks>
    /// A stored analysis is evidence, and evidence gets read long after it was written — by a gate, an
    /// auditor, or a person deciding whether a review still holds. Two things can be wrong with it and
    /// both are silent: the document can contradict itself, and it can be perfectly coherent but stale,
    /// describing a model that has since changed. This checks for both, because a clean report about
    /// last month's architecture is the more dangerous of the two.
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
            Console.Error.WriteLine("Validate a stored tmforge-analysis document.");
            Console.Error.WriteLine("Usage:");
            Console.Error.WriteLine("  tmforge analysis validate [--model <path>] [--expect-version <n>] [--json] <document>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  --model <path>          Also check the document against the model it describes, so a");
            Console.Error.WriteLine("                          coherent but stale analysis is reported instead of trusted.");
            Console.Error.WriteLine("  --expect-version <n>    Pin the schema version this caller was written against; any");
            Console.Error.WriteLine("                          other version is refused rather than read on wrong assumptions.");
            Console.Error.WriteLine("  --json                  Emit the result as JSON.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Exit codes: 0 = sound; 1 = tool error; 2 = problems found.");
        }
    }
}
