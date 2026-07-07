namespace ThreatModelForge.Editing
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A normalized, geometry-free description of a single model element (component, boundary, or
    /// flow): its identity, kind, owning diagram, and a flat bag of comparable attributes. This is
    /// the shared unit consumed by both the structural diff and the canonical text rendering.
    /// </summary>
    public sealed class ElementDescriptor
    {
        /// <summary>Gets the stable identifier of the element.</summary>
        public Guid Id { get; init; }

        /// <summary>Gets the element kind (<c>process</c>, <c>store</c>, <c>external</c>, <c>boundary</c>, <c>flow</c>, or the raw type id).</summary>
        public string Kind { get; init; } = string.Empty;

        /// <summary>Gets the element's display name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets the name of the diagram (page) the element belongs to.</summary>
        public string DiagramName { get; init; } = string.Empty;

        /// <summary>Gets the identifier of the diagram (page) the element belongs to.</summary>
        public Guid DiagramId { get; init; }

        /// <summary>
        /// Gets the comparable attributes of the element: the synthesized <c>name</c> and <c>kind</c>
        /// keys, the <c>source</c>/<c>target</c> endpoints of a flow, and every custom property.
        /// Geometry is deliberately excluded.
        /// </summary>
        public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>();
    }
}
