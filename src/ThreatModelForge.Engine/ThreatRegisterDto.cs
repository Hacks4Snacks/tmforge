namespace ThreatModelForge.Engine
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The threat register split by origin and by standing against the current analysis.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The counts are not a partition and must not be summed. Each answers a different question: what
    /// the rules produce now, what the model stores, what people wrote by hand, and what is left over.
    /// One threat can be counted in more than one.
    /// </para>
    /// <para>
    /// What "stored" means depends on the model's format. A <c>.tm7</c> carries the full generated
    /// register, so <see cref="PersistedGenerated"/> covers every rule-derived entry. Canonical
    /// tmforge-json deliberately persists only author-owned state, so on that surface it counts the
    /// rule threats someone triaged — which makes <see cref="StaleGenerated"/> the more useful number
    /// there: it is triage that no longer matches any threat the rules produce.
    /// </para>
    /// </remarks>
    public sealed class ThreatRegisterDto
    {
        /// <summary>Gets the number of entries an author wrote by hand.</summary>
        public int Manual { get; init; }

        /// <summary>Gets the number of stored rule-derived entries.</summary>
        public int PersistedGenerated { get; init; }

        /// <summary>Gets the number of threats the current rules produce, stored or not.</summary>
        public int CurrentGenerated { get; init; }

        /// <summary>Gets the number of stored entries the current rules no longer produce.</summary>
        public int StaleGenerated { get; init; }

        /// <summary>
        /// Gets the number of stored entries whose rule was not part of this run, so their standing
        /// could not be judged. They are excluded from <see cref="StaleGenerated"/> rather than assumed
        /// stale: a rule that never ran says nothing about the model.
        /// </summary>
        public int IndeterminateGenerated { get; init; }

        /// <summary>
        /// Gets the rule ids the register refers to that this run could not evaluate, sorted. A
        /// non-empty list means the effective bundle does not match the one that wrote the register.
        /// </summary>
        public IReadOnlyList<string> UnavailableRuleIds { get; init; } = Array.Empty<string>();

        /// <summary>Gets every register entry with its classification.</summary>
        public IReadOnlyList<ThreatRegisterEntryDto> Entries { get; init; } = Array.Empty<ThreatRegisterEntryDto>();

        /// <summary>Gets the rule-loading diagnostics for this run.</summary>
        public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();

        /// <summary>Gets the effective custom rule packs this run evaluated.</summary>
        public IReadOnlyList<RulePackInfoDto> RulePacks { get; init; } = Array.Empty<RulePackInfoDto>();
    }
}
