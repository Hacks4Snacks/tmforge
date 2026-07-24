namespace ThreatModelForge.Engine
{
    /// <summary>
    /// A custom rule pack the model expects to be analyzed with. It pins content, not just a name: when
    /// the pack is absent from the effective bundle, or its fingerprint differs, the engine reports the
    /// mismatch instead of quietly analyzing against different rules.
    /// </summary>
    public sealed class ExpectedRulePackDto
    {
        /// <summary>Gets the expected pack identifier.</summary>
        public string? Id { get; init; }

        /// <summary>Gets the expected content fingerprint, or <see langword="null"/> to accept any content.</summary>
        public string? Fingerprint { get; init; }
    }
}
