namespace ThreatModelForge.Engine
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The custom rule content a host actually loaded, with the diagnostics raised while loading it.
    /// Callers use this to confirm that the pack they configured is the pack that runs, instead of
    /// discovering a silent fallback to built-in rules only.
    /// </summary>
    public sealed class RuleBundleDto
    {
        /// <summary>Gets the custom rule packs that contributed rules to the effective rule set.</summary>
        public IReadOnlyList<RulePackInfoDto> RulePacks { get; init; } = Array.Empty<RulePackInfoDto>();

        /// <summary>Gets the non-fatal rule load and validation warnings.</summary>
        public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
    }
}
