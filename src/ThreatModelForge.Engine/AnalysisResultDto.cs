namespace ThreatModelForge.Engine
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The result of an analysis run together with the evidence of which rule content produced it: the
    /// custom packs that actually loaded and any diagnostics the loader raised. A caller can therefore
    /// tell a clean model from a model analyzed with the wrong (or no) custom rules.
    /// </summary>
    public sealed class AnalysisResultDto
    {
        /// <summary>Gets the findings produced by the effective rule set.</summary>
        public IReadOnlyList<FindingDto> Findings { get; init; } = Array.Empty<FindingDto>();

        /// <summary>
        /// Gets the threats projected from the same evaluation that produced <see cref="Findings"/>.
        /// It is empty when the caller asked only for findings, which is how a findings-only request
        /// avoids materializing a threat register it will not read.
        /// </summary>
        public IReadOnlyList<ThreatDto> Threats { get; init; } = Array.Empty<ThreatDto>();

        /// <summary>Gets the custom rule packs that contributed rules to this run.</summary>
        public IReadOnlyList<RulePackInfoDto> RulePacks { get; init; } = Array.Empty<RulePackInfoDto>();

        /// <summary>Gets the non-fatal rule load and validation warnings raised by this run.</summary>
        public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
    }
}
