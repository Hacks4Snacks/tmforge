namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The trust-boundary crossings that changed between two revisions of a model.
    /// </summary>
    public sealed class CrossingDifference
    {
        /// <summary>Gets the flows whose crossings changed, ordered by diagram and then by flow name.</summary>
        public IReadOnlyList<CrossingChange> Changes { get; init; } = Array.Empty<CrossingChange>();

        /// <summary>Gets a value indicating whether nothing crosses differently.</summary>
        public bool IsEmpty => this.Changes.Count == 0;
    }
}
