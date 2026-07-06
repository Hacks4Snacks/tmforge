namespace ThreatModelForge.Editing
{
    /// <summary>
    /// Tunable spacing parameters for <see cref="DiagramLayout"/>. Defaults produce a readable
    /// layered diagram; callers may widen the gaps for larger stencils.
    /// </summary>
    public sealed class LayoutOptions
    {
        /// <summary>
        /// Gets or sets the x coordinate of the top-left origin of the laid-out region.
        /// </summary>
        public int OriginX { get; set; } = 40;

        /// <summary>
        /// Gets or sets the y coordinate of the top-left origin of the laid-out region.
        /// </summary>
        public int OriginY { get; set; } = 40;

        /// <summary>
        /// Gets or sets the horizontal gap between adjacent layers (columns).
        /// </summary>
        public int LayerSpacing { get; set; } = 80;

        /// <summary>
        /// Gets or sets the vertical gap between adjacent nodes within a layer.
        /// </summary>
        public int NodeSpacing { get; set; } = 40;
    }
}
