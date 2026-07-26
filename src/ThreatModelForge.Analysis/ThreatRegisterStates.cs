namespace ThreatModelForge.Analysis
{
    /// <summary>
    /// How a threat register entry came to exist, and where it stands relative to the current analysis.
    /// </summary>
    /// <remarks>
    /// The register is append-only by design: applying a generation result never deletes, so triage is
    /// never lost when a rule stops firing. The cost is that a left-over entry is indistinguishable
    /// from a current one unless it is classified, which is what these states are for.
    /// </remarks>
    public static class ThreatRegisterStates
    {
        /// <summary>An entry an author wrote by hand. Rules never produce or retire it.</summary>
        public const string Manual = "manual";

        /// <summary>A rule-derived entry the current rules still produce.</summary>
        public const string CurrentGenerated = "current-generated";

        /// <summary>
        /// A rule-derived entry whose rule is present and enabled but no longer produces it, so the
        /// finding it recorded has genuinely gone away.
        /// </summary>
        public const string StaleGenerated = "stale-generated";

        /// <summary>
        /// A rule-derived entry whose rule is absent from the effective bundle or disabled for this
        /// run. It cannot be called stale: the rule was never given the chance to fire, so its silence
        /// says nothing about the model.
        /// </summary>
        public const string IndeterminateGenerated = "indeterminate-generated";

        /// <summary>Determines whether a value is one of the declared states.</summary>
        /// <param name="state">The candidate state.</param>
        /// <returns><see langword="true"/> when the value is declared.</returns>
        public static bool IsDefined(string? state) =>
            state == Manual ||
            state == CurrentGenerated ||
            state == StaleGenerated ||
            state == IndeterminateGenerated;
    }
}
