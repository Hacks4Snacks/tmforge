namespace ThreatModelForge.Editing
{
    using System.Collections.Generic;

    /// <summary>
    /// The structural difference between two threat models: the elements added, removed, and modified
    /// between them, matched by their stable identifiers.
    /// </summary>
    public sealed class ModelDifference
    {
        /// <summary>Gets the elements present only in the revised model.</summary>
        public IReadOnlyList<ElementChange> Added { get; init; } = new List<ElementChange>();

        /// <summary>Gets the elements present only in the base model.</summary>
        public IReadOnlyList<ElementChange> Removed { get; init; } = new List<ElementChange>();

        /// <summary>Gets the elements present in both models whose attributes differ.</summary>
        public IReadOnlyList<ElementChange> Modified { get; init; } = new List<ElementChange>();

        /// <summary>Gets a value indicating whether the two models are structurally identical.</summary>
        public bool IsEmpty => this.Added.Count == 0 && this.Removed.Count == 0 && this.Modified.Count == 0;
    }
}
