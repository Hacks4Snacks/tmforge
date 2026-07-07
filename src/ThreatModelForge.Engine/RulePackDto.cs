namespace ThreatModelForge.Engine
{
    /// <summary>
    /// Describes a rule pack: a named, selectable group of related analysis rules (for example, the
    /// data-protection pack). The validation UI uses packs so a model can enable or disable whole
    /// families of rules at once.
    /// </summary>
    public sealed class RulePackDto
    {
        /// <summary>Gets the stable pack identifier (for example, <c>data-protection</c>).</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the human-readable pack name shown in the validation UI.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets the number of rules the pack contributes.</summary>
        public int Count { get; init; }
    }
}
