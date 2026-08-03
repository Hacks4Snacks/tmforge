namespace ThreatModelForge.Engine
{
    /// <summary>
    /// The request body for applying a declarative authoring manifest.
    /// <para>
    /// The manifest travels as text rather than as a bound <see cref="Engine.Manifest"/> so its
    /// envelope is checked exactly as written. Binding first would defeat the check: every member of
    /// the manifest is optional, so a model or a rule pack would bind to an empty manifest and apply
    /// as an empty model. This mirrors how <see cref="RuleSourceDto"/> carries a rule pack.
    /// </para>
    /// </summary>
    public sealed class ManifestRequestDto
    {
        /// <summary>Gets the manifest document text.</summary>
        public string? Manifest { get; init; }

        /// <summary>
        /// Gets a value indicating whether to store property values the schema does not recognize
        /// instead of refusing the manifest.
        /// </summary>
        public bool Force { get; init; }
    }
}
