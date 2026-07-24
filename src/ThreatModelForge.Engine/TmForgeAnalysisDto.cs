namespace ThreatModelForge.Engine
{
    using System.Collections.Generic;

    /// <summary>
    /// Per-model analysis configuration that travels with the model, so the Studio and the CLI
    /// analyze against the same set of rules. It selects which analysis rule packs or individual
    /// rules to skip; an absent or empty selection runs every rule.
    /// </summary>
    public sealed class TmForgeAnalysisDto
    {
        /// <summary>Gets the ids of rule packs to skip (for example, <c>stride-completeness</c>).</summary>
        public IReadOnlyList<string>? DisabledPacks { get; init; }

        /// <summary>Gets the ids of individual rules to skip (for example, <c>TM1002</c>).</summary>
        public IReadOnlyList<string>? DisabledRuleIds { get; init; }

        /// <summary>
        /// Gets the custom rule packs this model expects to be analyzed with. A pack that is missing
        /// from the effective bundle, or whose content fingerprint differs, is reported as an error
        /// finding rather than silently ignored.
        /// </summary>
        public IReadOnlyList<ExpectedRulePackDto>? ExpectedPacks { get; init; }
    }
}
