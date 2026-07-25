namespace ThreatModelForge.Engine
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A versioned, self-describing record of one analysis run: what was analyzed, what analyzed it,
    /// and what it concluded about every finding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the artifact meant to be stored and compared between runs. It is deliberately separate
    /// from the CLI's <c>--json</c> envelope, which versions the command-line contract rather than the
    /// evidence, and from <see cref="AnalysisResultDto"/>, which is the transient shape a client
    /// renders.
    /// </para>
    /// <para>
    /// The document carries no timestamp. Two analyses of the same model with the same rules produce
    /// byte-identical documents, which is what lets a consumer diff them to see what actually changed
    /// rather than wading through churn. When a run happened is something the CI system already
    /// records, and putting it here would cost the property the artifact exists for.
    /// </para>
    /// </remarks>
    public sealed class AnalysisDocumentDto
    {
        /// <summary>The schema tag every <c>tmforge-analysis</c> document carries.</summary>
        public const string SchemaName = "tmforge-analysis";

        /// <summary>The current schema version.</summary>
        public const int CurrentVersion = 1;

        /// <summary>Gets the schema tag.</summary>
        public string Schema { get; init; } = SchemaName;

        /// <summary>Gets the schema version, so a reader can refuse or migrate what it does not know.</summary>
        public int Version { get; init; } = CurrentVersion;

        /// <summary>Gets the analyzed model's name and structural fingerprint.</summary>
        public AnalysisIdentityDto Model { get; init; } = new AnalysisIdentityDto();

        /// <summary>
        /// Gets the analyzer's identity and the fingerprint of the effective rule selection, so a
        /// consumer can tell whether a stored analysis was produced by the same rules it would run now.
        /// </summary>
        public AnalysisIdentityDto Analyzer { get; init; } = new AnalysisIdentityDto();

        /// <summary>Gets the custom rule packs that contributed rules to this run.</summary>
        public IReadOnlyList<RulePackInfoDto> RulePacks { get; init; } = Array.Empty<RulePackInfoDto>();

        /// <summary>Gets every finding, each with exactly one disposition.</summary>
        public IReadOnlyList<AnalysisFindingDto> Findings { get; init; } = Array.Empty<AnalysisFindingDto>();

        /// <summary>Gets non-fatal problems encountered while loading rule content.</summary>
        public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
    }
}
