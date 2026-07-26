namespace ThreatModelForge.Engine
{
    using System.Collections.Generic;

    /// <summary>
    /// The inputs for <see cref="AuthoringService.AddThreat"/>: a manually-authored STRIDE threat to
    /// record on the model's author overlay.
    /// </summary>
    public sealed class AddThreatRequest
    {
        /// <summary>
        /// Gets the author's id for this threat, with or without the <c>manual:</c> prefix, or
        /// <see langword="null"/> to have one generated. Supplying an id lets the threat be referenced
        /// from outside the model and keeps repeated authoring of the same threat idempotent.
        /// </summary>
        public string? Id { get; init; }

        /// <summary>Gets the threat title (statement).</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>Gets the STRIDE category.</summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>Gets the element/flow ids the threat is scoped to; empty or absent for a model-wide threat.</summary>
        public IReadOnlyList<string>? ElementIds { get; init; }

        /// <summary>Gets the initial lifecycle state (<c>Open</c> by default).</summary>
        public string? State { get; init; }

        /// <summary>Gets the priority (<c>High</c> / <c>Medium</c> / <c>Low</c>).</summary>
        public string? Priority { get; init; }

        /// <summary>Gets the description.</summary>
        public string? Description { get; init; }

        /// <summary>Gets the suggested mitigation.</summary>
        public string? Mitigation { get; init; }
    }
}
