namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The set of trust boundaries one flow crosses on one diagram.
    /// </summary>
    /// <remarks>
    /// This is derived state, not stored state. Which boundaries a flow crosses follows from where the
    /// flow's endpoints sit relative to each boundary's geometry, so it changes when someone drags an
    /// element even though no stored property changed. That is exactly why it is captured separately:
    /// the structural diff ignores geometry on purpose, and would otherwise report nothing at all.
    /// </remarks>
    public sealed class FlowCrossings
    {
        /// <summary>Gets the flow's stable id.</summary>
        public Guid FlowId { get; init; }

        /// <summary>Gets the flow's display name.</summary>
        public string FlowName { get; init; } = string.Empty;

        /// <summary>Gets the name of the diagram the flow is drawn on.</summary>
        public string DiagramName { get; init; } = string.Empty;

        /// <summary>Gets the id of the diagram the flow is drawn on.</summary>
        public Guid DiagramId { get; init; }

        /// <summary>Gets the boundaries the flow crosses, ordered by id.</summary>
        public IReadOnlyList<CrossedBoundary> Boundaries { get; init; } = Array.Empty<CrossedBoundary>();
    }
}
