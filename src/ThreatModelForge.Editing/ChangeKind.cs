namespace ThreatModelForge.Editing
{
    /// <summary>
    /// Classifies how a single element differs between two threat models.
    /// </summary>
    public enum ChangeKind
    {
        /// <summary>
        /// The element is present only in the right-hand (revised) model.
        /// </summary>
        Added,

        /// <summary>
        /// The element is present only in the left-hand (base) model.
        /// </summary>
        Removed,

        /// <summary>
        /// The element is present in both models, but one or more of its attributes differ.
        /// </summary>
        Modified,
    }
}
