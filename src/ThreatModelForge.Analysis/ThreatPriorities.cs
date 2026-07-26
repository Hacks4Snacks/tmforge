namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The canonical threat priority vocabulary.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Priority is author-owned: it records how urgently a threat should be addressed, which is a
    /// judgement about the system rather than a property of the rule that found it. Analysis gates on
    /// finding severity; priority never affects detection.
    /// </para>
    /// <para>
    /// <see cref="ThreatPriority"/> is the single source of truth. Every surface that accepts a
    /// priority canonicalizes through <see cref="TryCanonicalize"/>, and the knowledge base embedded
    /// in an exported <c>.tm7</c> declares exactly <see cref="All"/> — so any priority Threat Model
    /// Forge can express is one the Microsoft Threat Modeling Tool also offers, and a round trip
    /// through the tool cannot quietly coerce it to something else.
    /// </para>
    /// </remarks>
    public static class ThreatPriorities
    {
        /// <summary>
        /// Gets the canonical priority names, most urgent first. This is the order the tool presents
        /// them in.
        /// </summary>
        public static IReadOnlyList<string> All { get; } =
            Enum.GetValues(typeof(ThreatPriority))
                .Cast<ThreatPriority>()
                .Select(priority => priority.ToString())
                .ToArray();

        /// <summary>A human-readable list of the accepted values, for error messages.</summary>
        /// <returns>The accepted priorities, in declaration order.</returns>
        public static string Describe() => string.Join(", ", All);

        /// <summary>Canonicalizes a caller-supplied priority to its declared spelling.</summary>
        /// <param name="value">The supplied priority; blank means "unspecified".</param>
        /// <param name="priority">
        /// The canonical spelling, or <see langword="null"/> when <paramref name="value"/> was blank.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when the value was blank or recognized; <see langword="false"/> when
        /// it named a priority that does not exist.
        /// </returns>
        public static bool TryCanonicalize(string? value, out string? priority)
        {
            priority = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            priority = All.FirstOrDefault(candidate =>
                string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase));
            return priority != null;
        }
    }
}
