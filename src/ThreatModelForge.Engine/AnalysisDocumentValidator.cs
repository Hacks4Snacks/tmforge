namespace ThreatModelForge.Engine
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Checks that a <c>tmforge-analysis</c> document is internally coherent before a consumer relies
    /// on it.
    /// </summary>
    /// <remarks>
    /// A stored analysis is evidence, and evidence that contradicts itself is worse than none: a
    /// finding with no disposition silently drops out of a reconciliation, two findings sharing an id
    /// collapse onto one row, and a threat-bearing finding with no threat link cannot be joined to the
    /// register it is supposed to feed. The validator reports every problem it finds rather than
    /// stopping at the first, because these usually arrive in groups.
    /// </remarks>
    public static class AnalysisDocumentValidator
    {
        /// <summary>Validates a document.</summary>
        /// <param name="document">The document to validate.</param>
        /// <returns>The problems found, empty when the document is coherent.</returns>
        public static IReadOnlyList<string> Validate(AnalysisDocumentDto document)
        {
            _ = document ?? throw new ArgumentNullException(nameof(document));

            List<string> problems = new List<string>();
            ValidateEnvelope(document, problems);

            HashSet<string> packIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (RulePackInfoDto pack in document.RulePacks ?? Array.Empty<RulePackInfoDto>())
            {
                if (pack != null && !string.IsNullOrEmpty(pack.Id))
                {
                    packIds.Add(pack.Id);
                }
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            int index = 0;
            foreach (AnalysisFindingDto finding in document.Findings ?? Array.Empty<AnalysisFindingDto>())
            {
                string where = "findings[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                index++;

                if (finding == null)
                {
                    problems.Add($"{where} is null.");
                    continue;
                }

                ValidateIdentity(finding, where, seen, problems);
                ValidateDisposition(finding, where, problems);
                ValidateRuleReference(finding, where, packIds, problems);
            }

            return problems;
        }

        private static void ValidateEnvelope(AnalysisDocumentDto document, ICollection<string> problems)
        {
            if (!string.Equals(document.Schema, AnalysisDocumentDto.SchemaName, StringComparison.Ordinal))
            {
                problems.Add($"Expected schema '{AnalysisDocumentDto.SchemaName}' but found '{document.Schema}'.");
            }

            if (document.Version != AnalysisDocumentDto.CurrentVersion)
            {
                problems.Add(
                    $"Unsupported analysis schema version {document.Version.ToString(CultureInfo.InvariantCulture)}; " +
                    $"this build reads version {AnalysisDocumentDto.CurrentVersion.ToString(CultureInfo.InvariantCulture)}.");
            }

            if (string.IsNullOrWhiteSpace(document.Model?.Fingerprint))
            {
                problems.Add("The document records no model fingerprint, so it cannot be checked against a model.");
            }

            if (string.IsNullOrWhiteSpace(document.Analyzer?.Fingerprint))
            {
                problems.Add("The document records no analyzer fingerprint, so it cannot be checked against a rule set.");
            }
        }

        private static void ValidateIdentity(
            AnalysisFindingDto finding,
            string where,
            HashSet<string> seen,
            ICollection<string> problems)
        {
            if (string.IsNullOrWhiteSpace(finding.Id))
            {
                problems.Add($"{where} has no id.");
                return;
            }

            if (!seen.Add(finding.Id))
            {
                problems.Add($"{where} repeats the id '{finding.Id}', so two findings would reconcile onto one.");
            }
        }

        private static void ValidateDisposition(AnalysisFindingDto finding, string where, ICollection<string> problems)
        {
            if (string.IsNullOrWhiteSpace(finding.Disposition))
            {
                problems.Add($"{where} ('{finding.Id}') has no disposition.");
                return;
            }

            if (!FindingDispositions.IsDefined(finding.Disposition))
            {
                problems.Add($"{where} ('{finding.Id}') has unknown disposition '{finding.Disposition}'.");
                return;
            }

            bool threatBearing = FindingDispositions.IsThreatBearing(finding.Disposition);
            bool hasThreat = !string.IsNullOrWhiteSpace(finding.ThreatId);

            if (threatBearing && !hasThreat)
            {
                problems.Add(
                    $"{where} ('{finding.Id}') is '{finding.Disposition}' but links to no threat, " +
                    "so it cannot be joined to the register.");
            }

            if (!threatBearing && hasThreat)
            {
                problems.Add(
                    $"{where} ('{finding.Id}') is '{finding.Disposition}' yet links to threat " +
                    $"'{finding.ThreatId}'.");
            }
        }

        private static void ValidateRuleReference(
            AnalysisFindingDto finding,
            string where,
            ICollection<string> packIds,
            ICollection<string> problems)
        {
            if (string.IsNullOrWhiteSpace(finding.RuleId))
            {
                problems.Add($"{where} ('{finding.Id}') names no rule.");
                return;
            }

            // A pack-qualified rule id has to resolve to a pack the document says contributed, or the
            // evidence claims a rule ran that the document cannot account for.
            int separator = finding.RuleId.IndexOf('/');
            if (separator <= 0)
            {
                return;
            }

            string pack = finding.RuleId.Substring(0, separator);
            if (!packIds.Contains(pack))
            {
                problems.Add(
                    $"{where} ('{finding.Id}') came from rule pack '{pack}', which is not listed in rulePacks.");
            }
        }
    }
}
