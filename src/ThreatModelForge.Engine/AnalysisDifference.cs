namespace ThreatModelForge.Engine
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// What changed between two <c>tmforge-analysis</c> documents.
    /// </summary>
    public sealed class AnalysisDifference
    {
        /// <summary>Gets the findings the head analysis reports and the base analysis did not.</summary>
        public IReadOnlyList<AnalysisFindingDto> Introduced { get; init; } = Array.Empty<AnalysisFindingDto>();

        /// <summary>Gets the findings the base analysis reported and the head analysis does not.</summary>
        public IReadOnlyList<AnalysisFindingDto> Resolved { get; init; } = Array.Empty<AnalysisFindingDto>();

        /// <summary>Gets the findings present in both whose disposition or severity moved.</summary>
        public IReadOnlyList<AnalysisFindingChange> Reclassified { get; init; } = Array.Empty<AnalysisFindingChange>();

        /// <summary>Gets the number of findings present in both and classified identically.</summary>
        public int Unchanged { get; init; }

        /// <summary>
        /// Gets the conditions that make the comparison less meaningful than it looks — a changed rule
        /// selection, two different models, or a difference no recorded input explains.
        /// </summary>
        /// <remarks>
        /// These are reported rather than acted on. Refusing to compare would be unhelpful, and
        /// comparing silently would let a rule change read as a model change.
        /// </remarks>
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

        /// <summary>Gets a value indicating whether the two analyses reached the same conclusions.</summary>
        public bool IsEmpty =>
            this.Introduced.Count == 0 && this.Resolved.Count == 0 && this.Reclassified.Count == 0;
    }
}
