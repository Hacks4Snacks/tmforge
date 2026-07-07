namespace ThreatModelForge.Editing
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Describes how a single element (component, boundary, or flow) differs between two models:
    /// whether it was added, removed, or modified, and — for a modification — the per-attribute
    /// changes.
    /// </summary>
    public sealed class ElementChange
    {
        /// <summary>Gets the stable identifier of the element.</summary>
        public Guid Id { get; init; }

        /// <summary>Gets whether the element was added, removed, or modified.</summary>
        public ChangeKind Kind { get; init; }

        /// <summary>Gets the element kind (<c>process</c>, <c>store</c>, <c>external</c>, <c>boundary</c>, <c>flow</c>, or the raw type id).</summary>
        public string ElementKind { get; init; } = string.Empty;

        /// <summary>Gets the element's display name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets the name of the diagram (page) the element belongs to.</summary>
        public string DiagramName { get; init; } = string.Empty;

        /// <summary>Gets the identifier of the diagram (page) the element belongs to.</summary>
        public Guid DiagramId { get; init; }

        /// <summary>Gets the per-attribute changes; empty for an added or removed element.</summary>
        public IReadOnlyList<PropertyChange> PropertyChanges { get; init; } = new List<PropertyChange>();
    }
}
