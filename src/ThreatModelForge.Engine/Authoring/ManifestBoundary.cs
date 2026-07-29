namespace ThreatModelForge.Engine
{
    /// <summary>A trust boundary in a <see cref="Manifest"/>.</summary>
    public sealed class ManifestBoundary
    {
        /// <summary>Gets or sets the stable alias used to reference this boundary from elements.</summary>
        public string? Alias { get; set; }

        /// <summary>Gets or sets the boundary's display name.</summary>
        public string? Name { get; set; }

        /// <summary>Gets or sets the alias of the page this boundary is drawn on. Defaults to the first page.</summary>
        public string? Page { get; set; }

        /// <summary>
        /// Gets or sets the left edge. Optional: a boundary with no geometry is laid out automatically
        /// and sized to hold its members. Supplying it fixes the boundary in place.
        /// </summary>
        public int? X { get; set; }

        /// <summary>Gets or sets the top edge. See <see cref="X"/>.</summary>
        public int? Y { get; set; }

        /// <summary>Gets or sets the width. See <see cref="X"/>.</summary>
        public int? Width { get; set; }

        /// <summary>Gets or sets the height. See <see cref="X"/>.</summary>
        public int? Height { get; set; }
    }
}
