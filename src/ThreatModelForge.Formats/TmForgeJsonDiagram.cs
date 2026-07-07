namespace ThreatModelForge.Formats
{
    using System;

    /// <summary>
    /// A single named page (diagram) in a <see cref="TmForgeJsonModel"/>. Each page carries its own
    /// elements and flows and maps to one <see cref="ThreatModelForge.Model.DrawingSurfaceModel"/> in
    /// the canonical model. Cross-page flows are out of scope: a flow's endpoints are on the same page.
    /// </summary>
    public sealed class TmForgeJsonDiagram
    {
        /// <summary>Gets the stable page identifier.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the page (tab) label, for example <c>Context</c> or <c>Payments service</c>.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets the diagram elements on this page.</summary>
        public TmForgeJsonElement[] Elements { get; init; } = Array.Empty<TmForgeJsonElement>();

        /// <summary>Gets the directed data flows on this page.</summary>
        public TmForgeJsonFlow[] Flows { get; init; } = Array.Empty<TmForgeJsonFlow>();
    }
}
