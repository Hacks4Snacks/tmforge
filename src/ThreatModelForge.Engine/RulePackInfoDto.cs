namespace ThreatModelForge.Engine
{
    /// <summary>
    /// The identity of a custom rule pack that actually contributed rules to an engine operation.
    /// Callers compare this against what they expected to load, so a pack that silently failed to load
    /// or changed content is visible rather than mistaken for a clean run.
    /// </summary>
    public sealed class RulePackInfoDto
    {
        /// <summary>Gets the stable pack identifier.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the pack display name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets the declared pack version, if any.</summary>
        public string? Version { get; init; }

        /// <summary>Gets the content fingerprint computed from the loaded pack bytes.</summary>
        public string Fingerprint { get; init; } = string.Empty;

        /// <summary>Gets the rule language dialect the pack is written in.</summary>
        public string Dialect { get; init; } = string.Empty;

        /// <summary>Gets the number of rules the pack contributed to the effective rule set.</summary>
        public int RuleCount { get; init; }
    }
}
