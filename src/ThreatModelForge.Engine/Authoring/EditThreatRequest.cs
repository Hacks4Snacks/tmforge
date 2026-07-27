namespace ThreatModelForge.Engine
{
    /// <summary>
    /// The inputs for <see cref="AuthoringService.EditThreat"/>: the author-owned fields to change on an
    /// existing threat. Only the non-null fields are applied.
    /// </summary>
    public sealed class EditThreatRequest
    {
        /// <summary>Gets the threat id to edit (a register id from the threats projection, or a manual id).</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the new lifecycle state, or <see langword="null"/> to leave it unchanged.</summary>
        public string? State { get; init; }

        /// <summary>
        /// Gets the new title, or <see langword="null"/> to leave it unchanged. An empty string clears an
        /// override on a generated threat, returning it to the title its rule produces.
        /// </summary>
        public string? Title { get; init; }

        /// <summary>
        /// Gets the new category, or <see langword="null"/> to leave it unchanged. Only a manually
        /// authored threat has an author-owned category; a generated threat's category belongs to the
        /// rule that detected it, and editing it is refused rather than silently ignored.
        /// </summary>
        public string? Category { get; init; }

        /// <summary>Gets the new priority, or <see langword="null"/> to leave it unchanged.</summary>
        public string? Priority { get; init; }

        /// <summary>Gets the new description, or <see langword="null"/> to leave it unchanged.</summary>
        public string? Description { get; init; }

        /// <summary>Gets the new mitigation, or <see langword="null"/> to leave it unchanged.</summary>
        public string? Mitigation { get; init; }

        /// <summary>Gets the new justification / state note, or <see langword="null"/> to leave it unchanged.</summary>
        public string? Justification { get; init; }
    }
}
