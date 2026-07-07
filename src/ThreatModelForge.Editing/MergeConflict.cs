namespace ThreatModelForge.Editing
{
    using System;

    /// <summary>
    /// A single point where a three-way merge could not reconcile the two sides automatically. The
    /// merge keeps the <c>ours</c> value; this record tells the user what to review and resolve.
    /// </summary>
    public sealed class MergeConflict
    {
        /// <summary>Gets the identifier of the element the conflict concerns.</summary>
        public Guid ElementId { get; init; }

        /// <summary>Gets the element kind (<c>process</c>, <c>store</c>, <c>flow</c>, ...).</summary>
        public string ElementKind { get; init; } = string.Empty;

        /// <summary>Gets the element's display name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets the name of the diagram (page) the element belongs to.</summary>
        public string DiagramName { get; init; } = string.Empty;

        /// <summary>Gets the kind of conflict.</summary>
        public MergeConflictKind Kind { get; init; }

        /// <summary>Gets the attribute key in conflict, or an empty string for a structural conflict.</summary>
        public string Property { get; init; } = string.Empty;

        /// <summary>Gets the ancestor value, or <see langword="null"/> when not applicable.</summary>
        public string? Base { get; init; }

        /// <summary>Gets the <c>ours</c> value (which the merge kept), or <see langword="null"/>.</summary>
        public string? Ours { get; init; }

        /// <summary>Gets the <c>theirs</c> value (which the merge dropped), or <see langword="null"/>.</summary>
        public string? Theirs { get; init; }
    }
}
