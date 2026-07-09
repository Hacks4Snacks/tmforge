namespace ThreatModelForge.Engine
{
    using System.Collections.Generic;

    /// <summary>A data flow between two elements in a <see cref="Manifest"/>.</summary>
    public sealed class ManifestFlow
    {
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
