namespace ThreatModelForge.Engine
{
    using System.Collections.Generic;

    /// <summary>An element (process, data store, or external entity) in a <see cref="Manifest"/>.</summary>
    public sealed class ManifestElement
    {
        /// <summary>Gets or sets the stable alias used to reference this element from flows.</summary>
        public string? Alias { get; set; }

        /// <summary>Gets or sets the element kind (<c>process</c>, <c>store</c>, <c>external</c>, or <c>boundary</c>). Optional when <see cref="Stencil"/> is set.</summary>
        public string? Kind { get; set; }

        /// <summary>Gets or sets the element's display name.</summary>
        public string? Name { get; set; }

        /// <summary>Gets or sets the concrete stencil id (for example <c>k8s-configmap</c>); its base primitive determines the kind.</summary>
        public string? Stencil { get; set; }

        /// <summary>Gets or sets the alias of the trust boundary this element belongs to.</summary>
        public string? Boundary { get; set; }

        /// <summary>Gets or sets the typed custom properties the analyzer reads.</summary>
        public Dictionary<string, string>? Props { get; set; }
    }
}
