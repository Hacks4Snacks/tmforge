namespace ThreatModelForge.Editing
{
    using System.Collections.Generic;

    /// <summary>
    /// Describes an authoring stencil: a named, categorized specialization of one of the four DFD
    /// primitives (<c>process</c>, <c>datastore</c>, <c>external</c>, <c>boundary</c>). A stencil
    /// maps to its <see cref="Base"/> primitive for analysis, and its identity plus preset
    /// properties enrich the model without requiring per-stencil rules.
    /// </summary>
    public sealed class StencilDto
    {
        /// <summary>Gets the stable stencil identifier (for example, <c>azure-sql</c>).</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the underlying DFD primitive this stencil maps to.</summary>
        public string Base { get; init; } = string.Empty;

        /// <summary>Gets the human-readable name shown in the palette.</summary>
        public string Label { get; init; } = string.Empty;

        /// <summary>Gets the palette grouping this stencil belongs to.</summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>Gets the stencil pack this stencil ships in (for example, <c>azure</c>).</summary>
        public string Pack { get; init; } = string.Empty;

        /// <summary>Gets a short description of the stencil.</summary>
        public string Blurb { get; init; } = string.Empty;

        /// <summary>Gets the free-form search tags and aliases for this stencil.</summary>
        public IReadOnlyList<string> Tags { get; init; } = new List<string>();

        /// <summary>Gets the preset custom properties applied when the stencil is placed.</summary>
        public IReadOnlyDictionary<string, string> Defaults { get; init; } = new Dictionary<string, string>();
    }
}
