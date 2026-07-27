namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;
    using ThreatModelForge.Editing;

    /// <summary>
    /// How one flow's trust-boundary crossings changed between two revisions of a model.
    /// </summary>
    public sealed class CrossingChange
    {
        /// <summary>Gets the flow's stable id.</summary>
        public Guid FlowId { get; init; }

        /// <summary>Gets the flow's display name, taken from the revision it exists in.</summary>
        public string FlowName { get; init; } = string.Empty;

        /// <summary>Gets the name of the diagram the flow is drawn on.</summary>
        public string DiagramName { get; init; } = string.Empty;

        /// <summary>
        /// Gets what happened to the flow itself: <see cref="ChangeKind.Added"/> and
        /// <see cref="ChangeKind.Removed"/> mean the flow appeared or disappeared, so its crossings are
        /// reported wholesale; <see cref="ChangeKind.Modified"/> means the flow exists in both
        /// revisions and only what it crosses changed.
        /// </summary>
        public ChangeKind Kind { get; init; } = ChangeKind.Modified;

        /// <summary>Gets the boundaries the flow crosses now but did not cross before, ordered by id.</summary>
        public IReadOnlyList<CrossedBoundary> Added { get; init; } = Array.Empty<CrossedBoundary>();

        /// <summary>Gets the boundaries the flow crossed before but no longer crosses, ordered by id.</summary>
        public IReadOnlyList<CrossedBoundary> Removed { get; init; } = Array.Empty<CrossedBoundary>();
    }
}
