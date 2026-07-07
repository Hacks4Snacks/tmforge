namespace ThreatModelForge.Engine
{
    /// <summary>
    /// A single point where a three-way merge could not reconcile the two sides automatically. The
    /// merge keeps the <c>ours</c> value; this tells the caller what to review and resolve.
    /// </summary>
    public sealed class MergeConflictDto
    {
        /// <summary>Gets the identifier of the element the conflict concerns.</summary>
        public string? ElementId { get; init; }

        /// <summary>Gets the element kind (<c>process</c>, <c>store</c>, <c>flow</c>, ...).</summary>
        public string? ElementKind { get; init; }

        /// <summary>Gets the element's display name.</summary>
        public string? Name { get; init; }

        /// <summary>Gets the name of the diagram (page) the element belongs to.</summary>
        public string? DiagramName { get; init; }

        /// <summary>Gets the conflict kind: <c>Property</c>, <c>DeleteModify</c>, <c>AddAdd</c>, or <c>DanglingReference</c>.</summary>
        public string? Kind { get; init; }

        /// <summary>Gets the attribute key in conflict, or an empty string for a structural conflict.</summary>
        public string? Property { get; init; }

        /// <summary>Gets the ancestor value, or <see langword="null"/> when not applicable.</summary>
        public string? Base { get; init; }

        /// <summary>Gets the <c>ours</c> value the merge kept, or <see langword="null"/>.</summary>
        public string? Ours { get; init; }

        /// <summary>Gets the <c>theirs</c> value the merge dropped, or <see langword="null"/>.</summary>
        public string? Theirs { get; init; }
    }
}
