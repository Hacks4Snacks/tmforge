namespace ThreatModelForge.Engine
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The inputs for <see cref="AuthoringOperations.Set"/>: the element or flow reference, an
    /// optional new name and page scope, and the typed property assignments to apply.
    /// </summary>
    public sealed class SetRequest
    {
        /// <summary>Gets the element or flow reference (GUID, alias, or unique name).</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the new display name, or <see langword="null"/> to leave the name unchanged.</summary>
        public string? Name { get; init; }

        /// <summary>Gets the page to scope the search to (a 1-based index or a page name); when omitted every page is searched.</summary>
        public string? Page { get; init; }

        /// <summary>Gets the <c>KEY=VALUE</c> custom-property assignments to apply.</summary>
        public IReadOnlyList<string> Properties { get; init; } = Array.Empty<string>();

        /// <summary>Gets a value indicating whether to store unknown/invalid property values instead of rejecting them.</summary>
        public bool Force { get; init; }
    }
}
