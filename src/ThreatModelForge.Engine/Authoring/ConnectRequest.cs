namespace ThreatModelForge.Engine
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The inputs for <see cref="AuthoringOperations.Connect"/>: the source and target element
    /// references, an optional flow name and page, and typed property assignments to stamp on the
    /// new data flow.
    /// </summary>
    public sealed class ConnectRequest
    {
        /// <summary>Gets the source element reference (GUID, alias, or unique name).</summary>
        public string Source { get; init; } = string.Empty;

        /// <summary>Gets the target element reference (GUID, alias, or unique name).</summary>
        public string Target { get; init; } = string.Empty;

        /// <summary>Gets the flow's display name.</summary>
        public string? Name { get; init; }

        /// <summary>Gets the target page (a 1-based index or a page name); when omitted the first page is used.</summary>
        public string? Page { get; init; }

        /// <summary>Gets the <c>KEY=VALUE</c> custom-property assignments to apply.</summary>
        public IReadOnlyList<string> Properties { get; init; } = Array.Empty<string>();

        /// <summary>Gets a value indicating whether to store unknown/invalid property values instead of rejecting them.</summary>
        public bool Force { get; init; }
    }
}
