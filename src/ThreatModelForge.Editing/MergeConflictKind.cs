namespace ThreatModelForge.Editing
{
    /// <summary>
    /// Classifies why the two sides of a three-way merge could not be reconciled automatically.
    /// </summary>
    public enum MergeConflictKind
    {
        /// <summary>
        /// Both sides changed the same attribute of the same element to different values.
        /// </summary>
        Property,

        /// <summary>
        /// One side deleted an element that the other side modified.
        /// </summary>
        DeleteModify,

        /// <summary>
        /// Both sides independently added an element with the same identifier but different content.
        /// </summary>
        AddAdd,

        /// <summary>
        /// A flow endpoint refers to an element that the merge removed.
        /// </summary>
        DanglingReference,
    }
}
