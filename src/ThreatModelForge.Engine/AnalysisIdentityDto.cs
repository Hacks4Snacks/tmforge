namespace ThreatModelForge.Engine
{
    /// <summary>
    /// Names what produced or was analyzed by an analysis run: a display name, an optional version,
    /// and a content fingerprint.
    /// </summary>
    /// <remarks>
    /// The fingerprint is what makes an analysis artifact checkable rather than merely descriptive. A
    /// consumer holding a stored analysis can recompute these and tell whether the artifact still
    /// describes the current model and rule set, instead of assuming it does.
    /// </remarks>
    public sealed class AnalysisIdentityDto
    {
        /// <summary>Gets the display name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets the version, when the subject carries one.</summary>
        public string? Version { get; init; }

        /// <summary>Gets the <c>sha256:</c>-prefixed content fingerprint.</summary>
        public string Fingerprint { get; init; } = string.Empty;
    }
}
