namespace ThreatModelForge.Analysis
{
    using System.Collections.Generic;

    /// <summary>
    /// The threat register split by origin and by standing relative to the current analysis.
    /// </summary>
    /// <remarks>
    /// The counts are deliberately not a partition and must not be summed. Each answers a different
    /// question: what the rules produce now, what the model has stored, what people wrote by hand, and
    /// what is left over. A single threat can be counted in more than one of them.
    /// </remarks>
    public sealed class ThreatRegisterSummary
    {
        /// <summary>Gets the number of entries an author wrote by hand.</summary>
        public int Manual { get; init; }

        /// <summary>Gets the number of rule-derived entries stored in the register.</summary>
        public int PersistedGenerated { get; init; }

        /// <summary>
        /// Gets the number of threats the current rules produce, whether or not they are stored. This
        /// is what <c>tmforge threats</c> reports and can exceed <see cref="PersistedGenerated"/> when
        /// the register has not been written since the model changed.
        /// </summary>
        public int CurrentGenerated { get; init; }

        /// <summary>Gets the number of stored entries the current rules no longer produce.</summary>
        public int StaleGenerated { get; init; }

        /// <summary>
        /// Gets the number of stored entries that cannot be judged because their rule was not part of
        /// this run. These are excluded from <see cref="StaleGenerated"/> rather than assumed stale.
        /// </summary>
        public int IndeterminateGenerated { get; init; }

        /// <summary>
        /// Gets the rule ids the register refers to that this run could not evaluate, sorted. A
        /// non-empty list means the effective bundle does not match the one that wrote the register.
        /// </summary>
        public IReadOnlyList<string> UnavailableRuleIds { get; init; } = new List<string>();

        /// <summary>Gets every register entry with its classification, in register order.</summary>
        public IReadOnlyList<ThreatRegisterEntry> Entries { get; init; } = new List<ThreatRegisterEntry>();
    }
}
