namespace ThreatModelForge.Engine
{
    /// <summary>
    /// One threat register entry with its origin and its standing against the current analysis.
    /// </summary>
    public sealed class ThreatRegisterEntryDto
    {
        /// <summary>Gets the register key.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// Gets the entry's standing: <c>manual</c>, <c>current-generated</c>, <c>stale-generated</c>,
        /// or <c>indeterminate-generated</c>.
        /// </summary>
        public string State { get; init; } = string.Empty;

        /// <summary>Gets the rule that produced the entry, or <see langword="null"/> when authored.</summary>
        public string? RuleId { get; init; }

        /// <summary>Gets the threat title.</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>Gets the author-owned lifecycle state in the wire vocabulary.</summary>
        public string Triage { get; init; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether a reviewer recorded a decision on this entry. A stale entry
        /// carrying one holds a judgement the rule going quiet does not retire.
        /// </summary>
        public bool HasTriage { get; init; }
    }
}
