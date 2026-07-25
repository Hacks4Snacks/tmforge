namespace ThreatModelForge.Engine
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The structural disposition of a finding: what the analysis concluded should happen to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every finding has exactly one disposition. The point of recording it explicitly is to stop
    /// hygiene findings being pushed into the threat register just so they have somewhere to live:
    /// "this diagram has no trust boundary" is a real finding and is not a threat, and a reviewer
    /// should not have to accept it as a risk to clear a gate.
    /// </para>
    /// <para>
    /// The values form a partition, evaluated in order: a suppressed finding is suppressed whatever
    /// else it is; a finding from a rule that declares no threat category is hygiene; and the rest are
    /// threat-bearing, sub-divided by the author's triage.
    /// </para>
    /// </remarks>
    public static class FindingDispositions
    {
        /// <summary>A suppression silenced this finding; it is recorded rather than dropped.</summary>
        public const string Suppressed = "suppressed";

        /// <summary>
        /// The rule declares no threat category, so the finding is a modelling-quality observation
        /// rather than a risk. It stays a finding and never enters the threat register.
        /// </summary>
        public const string Hygiene = "hygiene";

        /// <summary>Threat-bearing and not yet triaged.</summary>
        public const string GeneratedThreat = "generated-threat";

        /// <summary>Threat-bearing and explicitly marked as needing investigation.</summary>
        public const string Unresolved = "unresolved";

        /// <summary>Threat-bearing and accepted as a risk, with the author's justification.</summary>
        public const string Accepted = "accepted";

        /// <summary>Threat-bearing and mitigated.</summary>
        public const string Mitigated = "mitigated";

        private static readonly HashSet<string> Known = new HashSet<string>(StringComparer.Ordinal)
        {
            Suppressed,
            Hygiene,
            GeneratedThreat,
            Unresolved,
            Accepted,
            Mitigated,
        };

        /// <summary>Gets every defined disposition, in reporting order.</summary>
        public static IReadOnlyList<string> All { get; } = new[]
        {
            GeneratedThreat,
            Unresolved,
            Accepted,
            Mitigated,
            Hygiene,
            Suppressed,
        };

        /// <summary>Returns whether a value is a defined disposition.</summary>
        /// <param name="value">The disposition to test.</param>
        /// <returns><see langword="true"/> when the value is defined.</returns>
        public static bool IsDefined(string? value) => value != null && Known.Contains(value);

        /// <summary>
        /// Returns whether a disposition describes a threat-bearing finding, which is the set that
        /// must carry a threat id.
        /// </summary>
        /// <param name="value">The disposition to test.</param>
        /// <returns><see langword="true"/> when the disposition is threat-bearing.</returns>
        public static bool IsThreatBearing(string? value) =>
            string.Equals(value, GeneratedThreat, StringComparison.Ordinal) ||
            string.Equals(value, Unresolved, StringComparison.Ordinal) ||
            string.Equals(value, Accepted, StringComparison.Ordinal) ||
            string.Equals(value, Mitigated, StringComparison.Ordinal);
    }
}
