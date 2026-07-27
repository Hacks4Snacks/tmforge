namespace ThreatModelForge.Analysis
{
    using System;

    /// <summary>
    /// A trust boundary that a flow crosses, identified by the stable id the boundary carries so a
    /// renamed boundary is still recognised as the same one.
    /// </summary>
    public sealed class CrossedBoundary
    {
        /// <summary>Gets the boundary's stable id.</summary>
        public Guid Id { get; init; }

        /// <summary>Gets the boundary's display name at the time it was captured.</summary>
        public string Name { get; init; } = string.Empty;
    }
}
