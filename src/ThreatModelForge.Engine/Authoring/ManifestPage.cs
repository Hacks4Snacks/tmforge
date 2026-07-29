namespace ThreatModelForge.Engine
{
    /// <summary>A page (diagram) in a <see cref="Manifest"/>.</summary>
    /// <remarks>
    /// Pages are optional. A manifest that declares none builds onto a single default page, which is
    /// what every manifest written before pages existed expects. Declaring them lets one manifest
    /// describe a context diagram plus per-service diagrams, the way a <c>.tm7</c> already can.
    /// </remarks>
    public sealed class ManifestPage
    {
        /// <summary>
        /// Gets or sets the stable alias elements and boundaries use to say which page they are on.
        /// It also fixes the page's identifier, which finding ids embed.
        /// </summary>
        public string? Alias { get; set; }

        /// <summary>Gets or sets the page's display name.</summary>
        public string? Name { get; set; }
    }
}
