namespace ThreatModelForge.Cli
{
    /// <summary>A trust boundary in a <see cref="Manifest"/>.</summary>
    internal sealed class ManifestBoundary
    {
        /// <summary>Gets or sets the stable alias used to reference this boundary from elements.</summary>
        public string? Alias { get; set; }

        /// <summary>Gets or sets the boundary's display name.</summary>
        public string? Name { get; set; }
    }
}
