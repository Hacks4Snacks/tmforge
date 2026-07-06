namespace ThreatModelForge.Editing
{
    /// <summary>
    /// The kinds of element that can be added to a diagram from the palette.
    /// </summary>
    public enum StencilKind
    {
        /// <summary>
        /// A process (drawn as an ellipse).
        /// </summary>
        Process,

        /// <summary>
        /// An external entity (drawn as a rectangle).
        /// </summary>
        ExternalEntity,

        /// <summary>
        /// A data store (drawn as parallel lines).
        /// </summary>
        DataStore,

        /// <summary>
        /// A trust boundary (drawn as a dashed box).
        /// </summary>
        TrustBoundary,
    }
}
