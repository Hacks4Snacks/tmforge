namespace ThreatModelForge.Engine
{
    using System.Collections.Generic;

    /// <summary>A data flow between two elements in a <see cref="Manifest"/>.</summary>
    public sealed class ManifestFlow
    {
        /// <summary>
        /// Gets or sets the flow's stable alias. Like an element alias this fixes the flow's identity,
        /// so re-applying a manifest reproduces the same connector id instead of a fresh one — which is
        /// what keeps finding ids, threat-register keys, and diffs aligned across runs.
        /// </summary>
        public string? Alias { get; set; }

        /// <summary>Gets or sets the source element reference (alias or unique name).</summary>
        public string? From { get; set; }

        /// <summary>Gets or sets the target element reference (alias or unique name).</summary>
        public string? To { get; set; }

        /// <summary>Gets or sets the flow's display name.</summary>
        public string? Name { get; set; }

        /// <summary>Gets or sets the typed custom properties the analyzer reads.</summary>
        public Dictionary<string, string>? Props { get; set; }
    }
}
