namespace ThreatModelForge.Engine
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One finding as it appears in a persisted <c>tmforge-analysis</c> document: the finding itself
    /// plus the structural conclusion drawn about it.
    /// </summary>
    /// <remarks>
    /// This is deliberately not <see cref="FindingDto"/>. That type is the transient shape a client
    /// renders on a canvas; this one is evidence meant to be stored, diffed, and reconciled, so it
    /// carries the disposition and the threat linkage and omits nothing a later run would need to
    /// match it up.
    /// </remarks>
    public sealed class AnalysisFindingDto
    {
        /// <summary>
        /// Gets the stable identity, <c>{ruleId}:{diagram}:{target}:{occurrence}</c>. This is the key a
        /// consumer reconciles on across runs.
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the reporting rule's effective id, pack-qualified for a custom rule.</summary>
        public string RuleId { get; init; } = string.Empty;

        /// <summary>Gets the severity (<c>info</c>, <c>warning</c>, or <c>error</c>).</summary>
        public string Severity { get; init; } = "info";

        /// <summary>Gets the human-readable finding text.</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>Gets the diagram key, or <see langword="null"/> for a model-wide finding.</summary>
        public string? Diagram { get; init; }

        /// <summary>Gets the element ids this finding is about, empty when it is about the model.</summary>
        public IReadOnlyList<string> ElementIds { get; init; } = Array.Empty<string>();

        /// <summary>Gets the disposition; see <see cref="FindingDispositions"/>.</summary>
        public string Disposition { get; init; } = FindingDispositions.Hygiene;

        /// <summary>
        /// Gets the register id of the threat this finding projects to, or <see langword="null"/> for a
        /// finding that is not threat-bearing. Threat-bearing findings always carry one, which is what
        /// lets a consumer join analysis evidence to the threat register.
        /// </summary>
        public string? ThreatId { get; init; }
    }
}
