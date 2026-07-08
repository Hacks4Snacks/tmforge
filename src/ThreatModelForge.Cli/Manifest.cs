namespace ThreatModelForge.Cli
{
    using System.Collections.Generic;

    /// <summary>
    /// The declarative authoring manifest: a review-friendly, text source of truth for a threat model
    /// that <c>tmforge apply</c> materializes into a model file and <c>tmforge export</c> emits from
    /// one. Elements are referenced by <see cref="ManifestElement.Alias"/> (or unique name), so the
    /// manifest is stable and diffable and needs no GUIDs.
    /// </summary>
    internal sealed class Manifest
    {
        /// <summary>Gets or sets the model title.</summary>
        public string? Name { get; set; }

        /// <summary>Gets or sets the trust boundaries.</summary>
        public List<ManifestBoundary>? Boundaries { get; set; }

        /// <summary>Gets or sets the elements (processes, data stores, external entities).</summary>
        public List<ManifestElement>? Elements { get; set; }

        /// <summary>Gets or sets the data flows between elements.</summary>
        public List<ManifestFlow>? Flows { get; set; }
    }
}
