namespace ThreatModelForge.Analysis
{
    using ThreatModelForge.KnowledgeBase;

    /// <summary>
    /// One threat register entry, classified against the current analysis.
    /// </summary>
    public sealed class ThreatRegisterEntry
    {
        /// <summary>Gets the register key.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the entry's state, one of <see cref="ThreatRegisterStates"/>.</summary>
        public string State { get; init; } = string.Empty;

        /// <summary>Gets the rule that produced the entry, or <see langword="null"/> when authored.</summary>
        public string? RuleId { get; init; }

        /// <summary>Gets the threat title.</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Gets the author-owned lifecycle state. Presentation surfaces map this to their own wire
        /// vocabulary; the register itself keeps the domain value.
        /// </summary>
        public ThreatState Triage { get; init; }

        /// <summary>Gets a value indicating whether the entry carries triage a reviewer recorded.</summary>
        /// <remarks>
        /// This is what makes a stale entry worth reporting rather than discarding: an entry someone
        /// investigated, mitigated, or accepted holds a decision, and removing it silently would erase
        /// that decision along with the finding.
        /// </remarks>
        public bool HasTriage { get; init; }
    }
}
