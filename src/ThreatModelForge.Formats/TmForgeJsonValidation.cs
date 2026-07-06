namespace ThreatModelForge.Formats
{
    using System.Collections.Generic;

    /// <summary>
    /// The per-model validation selection persisted inside a <c>tmforge-json</c> document: which
    /// analysis rule packs or individual rules to skip. It travels with the model so the editor and
    /// the CLI validate against the same rules.
    /// </summary>
    public sealed class TmForgeJsonValidation
    {
        /// <summary>Gets the ids of rule packs to skip (for example, <c>stride-completeness</c>).</summary>
        public IReadOnlyList<string>? DisabledPacks { get; init; }

        /// <summary>Gets the ids of individual rules to skip (for example, <c>TM1002</c>).</summary>
        public IReadOnlyList<string>? DisabledRuleIds { get; init; }
    }
}
