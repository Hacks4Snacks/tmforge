namespace ThreatModelForge.Engine
{
    /// <summary>
    /// Describes a single analysis rule offered by the engine, so the validation settings UI can
    /// list rules, show what they check, and toggle them on or off per model.
    /// </summary>
    public sealed class RuleDto
    {
        /// <summary>Gets the stable rule identifier (for example, <c>TM1014</c>).</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the identifier of the rule pack this rule belongs to.</summary>
        public string Pack { get; init; } = string.Empty;

        /// <summary>Gets the default severity (<c>info</c>, <c>warning</c>, or <c>error</c>).</summary>
        public string Severity { get; init; } = "info";

        /// <summary>Gets the human-readable description of what the rule evaluates and why.</summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>Gets the human-readable guidance on how to clear a finding from this rule.</summary>
        public string HelpText { get; init; } = string.Empty;

        /// <summary>Gets the documentation URL for the rule, if any.</summary>
        public string? HelpUri { get; init; }
    }
}
