namespace ThreatModelForge.Analysis
{
    using System.Collections.Generic;

    /// <summary>
    /// The outcome of removing stale entries from a threat register.
    /// </summary>
    public sealed class StaleRemovalResult
    {
        /// <summary>Gets the register keys that were removed.</summary>
        public IReadOnlyList<string> Removed { get; init; } = new List<string>();

        /// <summary>
        /// Gets the stale entries that were kept because they carry triage and the caller did not ask
        /// to discard it.
        /// </summary>
        /// <remarks>
        /// A stale entry that someone investigated, mitigated, or accepted holds a decision. The rule
        /// falling silent retires the finding, not the decision, so these are reported back rather than
        /// swept up with the rest.
        /// </remarks>
        public IReadOnlyList<string> RetainedWithTriage { get; init; } = new List<string>();
    }
}
