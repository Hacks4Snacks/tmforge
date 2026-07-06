namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The built-in rule packs: their identifiers plus presentation metadata (display name and order).
    /// This is the single source of truth consumed by hosts (e.g. the engine API) so pack names and
    /// ordering never drift from the rules that declare them. A rule pack is a named, selectable group
    /// of related rules (mirroring stencil packs) that a model can choose to validate against.
    /// </summary>
    public static class RulePackCatalog
    {
        /// <summary>Structural completeness and diagram-hygiene rules (connectivity, naming, size).</summary>
        public const string CoreHygiene = "core-hygiene";

        /// <summary>STRIDE modeling-completeness rules (trust boundaries, crossings, flow metadata).</summary>
        public const string StrideCompleteness = "stride-completeness";

        /// <summary>Rules that check inputs and outputs are validated/sanitized across trust boundaries.</summary>
        public const string InputValidation = "input-validation";

        /// <summary>Rules that check data-at-rest protection: encryption, access control, integrity, retention.</summary>
        public const string DataProtection = "data-protection";

        /// <summary>Rules that check data-in-transit protection across trust boundaries.</summary>
        public const string TransportSecurity = "transport-security";

        /// <summary>Rules that check authentication, least privilege, and access to components.</summary>
        public const string IdentityAccess = "identity-access";

        private static readonly IReadOnlyList<KeyValuePair<string, string>> OrderedPacks = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>(CoreHygiene, "Core Hygiene"),
            new KeyValuePair<string, string>(StrideCompleteness, "STRIDE Completeness"),
            new KeyValuePair<string, string>(InputValidation, "Input Validation"),
            new KeyValuePair<string, string>(DataProtection, "Data Protection"),
            new KeyValuePair<string, string>(TransportSecurity, "Transport Security"),
            new KeyValuePair<string, string>(IdentityAccess, "Identity & Access"),
        };

        /// <summary>
        /// Gets the rule packs in presentation order, each mapping its identifier to its display name.
        /// </summary>
        public static IReadOnlyList<KeyValuePair<string, string>> Ordered => OrderedPacks;

        /// <summary>
        /// Gets the display name for a pack identifier, falling back to the identifier itself when unknown.
        /// </summary>
        /// <param name="packId">The rule-pack identifier.</param>
        /// <returns>The human-readable display name for the pack.</returns>
        public static string DisplayName(string packId)
        {
            foreach (KeyValuePair<string, string> pack in OrderedPacks)
            {
                if (string.Equals(pack.Key, packId, StringComparison.Ordinal))
                {
                    return pack.Value;
                }
            }

            return packId;
        }
    }
}
