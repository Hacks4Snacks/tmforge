namespace ThreatModelForge.Engine
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using ThreatModelForge.Analysis;
    using ThreatModelForge.Formats;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Builds the versioned <c>tmforge-analysis</c> document and the fingerprints it carries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There are two producers of this artifact and there have to be: the engine derives it from a
    /// canonical model posted by a client, and the CLI derives it from a report over a <c>.tm7</c>.
    /// They key elements differently, because a <c>.tm7</c> has persisted guids while canonical JSON
    /// has the author's own ids, and neither can honestly use the other's keys.
    /// </para>
    /// <para>
    /// What they must not differ on is everything else, so the fingerprints and the envelope are built
    /// here for both. The disposition policy lives in <see cref="FindingDispositions.Classify"/> for
    /// the same reason.
    /// </para>
    /// </remarks>
    public static class AnalysisDocumentBuilder
    {
        private static readonly JsonSerializerOptions FingerprintOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>
        /// Fingerprints the structural model: the elements, flows, and pages the rules actually read.
        /// </summary>
        /// <remarks>
        /// The rule selection and the triage overlay are deliberately excluded. Selection belongs to the
        /// analyzer fingerprint, and triage is author state that changes what a finding's disposition
        /// says without changing what the model is — folding either in here would report "the model
        /// changed" every time somebody accepted a risk.
        /// </remarks>
        /// <param name="dto">The canonical model.</param>
        /// <returns>A <c>sha256:</c>-prefixed fingerprint.</returns>
        public static string ModelFingerprint(TmForgeModelDto dto)
        {
            _ = dto ?? throw new ArgumentNullException(nameof(dto));

            TmForgeModelDto structural = new TmForgeModelDto
            {
                Schema = dto.Schema,
                Version = dto.Version,
                Elements = dto.Elements,
                Flows = dto.Flows,
                Diagrams = dto.Diagrams,
            };

            return RulePackIdentity.CreateFingerprint(
                JsonSerializer.SerializeToUtf8Bytes(structural, FingerprintOptions));
        }

        /// <summary>
        /// Fingerprints a loaded model by projecting it through the canonical format first, so a model
        /// analyzed from a <c>.tm7</c> and the same model posted as canonical JSON are fingerprinted by
        /// the same definition rather than by whatever their source bytes happened to be.
        /// </summary>
        /// <param name="model">The loaded model.</param>
        /// <returns>A <c>sha256:</c>-prefixed fingerprint.</returns>
        public static string ModelFingerprint(ThreatModel model)
        {
            _ = model ?? throw new ArgumentNullException(nameof(model));

            using (MemoryStream output = new MemoryStream())
            {
                new TmForgeJsonFormat().Write(model, output);
                TmForgeModelDto dto = JsonSerializer.Deserialize<TmForgeModelDto>(output.ToArray(), FingerprintOptions)
                    ?? new TmForgeModelDto();
                return ModelFingerprint(dto);
            }
        }

        /// <summary>Fingerprints the effective rule catalog of a loaded rule set.</summary>
        /// <param name="name">The analyzer name.</param>
        /// <param name="version">The analyzer version.</param>
        /// <param name="ruleSet">The effective rule set, after any selection has been applied.</param>
        /// <returns>A <c>sha256:</c>-prefixed fingerprint.</returns>
        public static string AnalyzerFingerprint(string name, string version, RuleSet ruleSet)
        {
            _ = ruleSet ?? throw new ArgumentNullException(nameof(ruleSet));

            return AnalyzerFingerprint(
                name,
                version,
                ruleSet.Rules.Select(rule => Signature(rule.ID, rule.Severity, rule.Disabled)));
        }

        /// <summary>Fingerprints the effective rule catalog recorded in a report.</summary>
        /// <param name="name">The analyzer name.</param>
        /// <param name="version">The analyzer version.</param>
        /// <param name="rules">The rule reports from one analysis run.</param>
        /// <returns>A <c>sha256:</c>-prefixed fingerprint.</returns>
        public static string AnalyzerFingerprint(string name, string version, IEnumerable<RuleReport> rules)
        {
            _ = rules ?? throw new ArgumentNullException(nameof(rules));

            return AnalyzerFingerprint(
                name,
                version,
                rules.Select(rule => Signature(rule.ID, rule.Severity, rule.Disabled)));
        }

        /// <summary>
        /// Records a report produced over a loaded model as a <c>tmforge-analysis</c> document. This is
        /// the CLI's producer: it keys on the persisted guids, which is the identity a <c>.tm7</c>
        /// actually carries, and it is the only producer that sees suppressions.
        /// </summary>
        /// <param name="report">The report from one analysis run.</param>
        /// <param name="model">The analyzed model, which carries the threat register.</param>
        /// <param name="packs">The custom rule packs that contributed rules, if any.</param>
        /// <param name="taxonomy">An optional mapping to the engagement's own threat catalogue.</param>
        /// <returns>The analysis document.</returns>
        public static AnalysisDocumentDto FromReport(
            ModelReport report,
            ThreatModel model,
            IReadOnlyList<RulePackInfoDto>? packs = null,
            AnalysisTaxonomyMap? taxonomy = null)
        {
            _ = report ?? throw new ArgumentNullException(nameof(report));
            _ = model ?? throw new ArgumentNullException(nameof(model));

            string analyzerName = report.AnalysisTool?.Name ?? "ThreatModelForge.Analysis";
            string analyzerVersion = report.AnalysisTool?.Version ?? "0.0.0.0";

            return new AnalysisDocumentDto
            {
                Model = new AnalysisIdentityDto
                {
                    Name = report.ThreatModelName ?? "model",
                    Fingerprint = ModelFingerprint(model),
                },
                Analyzer = new AnalysisIdentityDto
                {
                    Name = analyzerName,
                    Version = analyzerVersion,
                    Fingerprint = AnalyzerFingerprint(analyzerName, analyzerVersion, report.RuleReports),
                },
                RulePacks = packs ?? Array.Empty<RulePackInfoDto>(),
                Findings = ProjectEvidence(report, model, taxonomy),
            };
        }

        private static string Signature(string? id, MessageSeverity severity, bool disabled)
        {
            return string.Concat(
                id ?? string.Empty,
                "|",
                severity.ToString(),
                "|",
                disabled ? "off" : "on");
        }

        private static string AnalyzerFingerprint(string name, string version, IEnumerable<string> signatures)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(name).Append('@').Append(version).Append('\n');

            // Ordered, because the same rules discovered in a different order are the same rule set.
            foreach (string signature in signatures.OrderBy(value => value, StringComparer.Ordinal))
            {
                builder.Append(signature).Append('\n');
            }

            return RulePackIdentity.CreateFingerprint(Encoding.UTF8.GetBytes(builder.ToString()));
        }

        private static IReadOnlyList<AnalysisFindingDto> ProjectEvidence(
            ModelReport report,
            ThreatModel model,
            AnalysisTaxonomyMap? taxonomy)
        {
            List<AnalysisFindingDto> evidence = new List<AnalysisFindingDto>();
            FindingIdentity identity = new FindingIdentity();

            foreach (RuleReport rule in report.RuleReports)
            {
                // Reported messages first, then suppressed ones, matching the order the SARIF writer
                // allocates identities in — the two artifacts describe the same run and must agree.
                foreach (RuleReportMessage message in rule.Messages)
                {
                    evidence.Add(ToEvidence(rule, message, model, identity, suppressed: false, taxonomy));
                }

                foreach (RuleReportMessage message in rule.SuppressedMessages)
                {
                    evidence.Add(ToEvidence(rule, message, model, identity, suppressed: true, taxonomy));
                }
            }

            return evidence;
        }

        private static AnalysisFindingDto ToEvidence(
            RuleReport rule,
            RuleReportMessage message,
            ThreatModel model,
            FindingIdentity identity,
            bool suppressed,
            AnalysisTaxonomyMap? taxonomy)
        {
            string? targetKey = message.TargetId?.ToString("N");

            // A suppressed finding is not threat-bearing: the author has said this one does not count,
            // so it must not also claim a place in the register.
            string? threatId = !suppressed && message.TargetId != null && !string.IsNullOrEmpty(rule.ThreatCategoryId)
                ? string.Format(CultureInfo.InvariantCulture, "{0:N}:{1}", message.TargetId.Value, rule.ID)
                : null;

            return new AnalysisFindingDto
            {
                Id = identity.Next(rule.ID, message.Diagram?.ToString("N"), targetKey),
                RuleId = rule.ID ?? string.Empty,
                Severity = rule.Severity.ToString().ToLowerInvariant(),
                Message = message.Text ?? string.Empty,
                Diagram = message.Diagram?.ToString("N"),
                ElementIds = targetKey == null ? Array.Empty<string>() : new[] { targetKey },
                Disposition = FindingDispositions.Classify(suppressed, threatId, Triage(model, threatId)),
                ThreatId = threatId,

                // Applied here, after everything else is decided, so the engagement's catalogue can
                // never influence what was detected or how it was dispositioned.
                CanonicalIds = taxonomy?.For(rule.ID) ?? Array.Empty<string>(),
            };
        }

        private static string? Triage(ThreatModel model, string? threatId)
        {
            if (threatId == null || !model.AllThreatsDictionary.TryGetValue(threatId, out Threat? threat))
            {
                return null;
            }

            return ThreatStateWire.ToWire(threat.State);
        }
    }
}
