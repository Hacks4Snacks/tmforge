namespace ThreatModelForge.Formats
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The <c>tmforge-json</c> document: the canvas's canonical in-memory model and the wire shape
    /// exchanged with clients. It carries diagram structure only (elements, flows, trust
    /// boundaries, names, and geometry), not knowledge-base attributes or generated threats.
    /// </summary>
    public sealed class TmForgeJsonModel
    {
        /// <summary>Gets the schema discriminator (always <c>tmforge-json</c>).</summary>
        public string Schema { get; init; } = "tmforge-json";

        /// <summary>Gets the document version.</summary>
        public string Version { get; init; } = "0.1";

        /// <summary>Gets the diagram elements (processes, data stores, external entities, boundaries).</summary>
        public TmForgeJsonElement[] Elements { get; init; } = Array.Empty<TmForgeJsonElement>();

        /// <summary>Gets the directed data flows between elements.</summary>
        public TmForgeJsonFlow[] Flows { get; init; } = Array.Empty<TmForgeJsonFlow>();

        /// <summary>
        /// Gets the named pages (diagrams). When present, this is the authoritative multi-page form and
        /// the top-level <see cref="Elements"/> and <see cref="Flows"/> mirror the first page for older,
        /// single-page readers. Absent (<see langword="null"/>) for single-page models.
        /// </summary>
        public IReadOnlyList<TmForgeJsonDiagram>? Diagrams { get; init; }

        /// <summary>Gets the per-model analysis selection (which rule packs or rules to skip).</summary>
        public TmForgeJsonAnalysis? Analysis { get; init; }
    }
}
