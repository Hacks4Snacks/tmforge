namespace ThreatModelForge.Engine
{
    using System;
    using System.Collections.Generic;
    using ThreatModelForge.Editing;

    /// <summary>
    /// The inputs for <see cref="AuthoringOperations.Add"/>: the resolved element kind (and optional
    /// stencil), placement, naming, boundary membership, alias, and typed property assignments to
    /// stamp on the new element. Callers resolve the kind/stencil via
    /// <see cref="AuthoringSupport.TryResolveKind"/> before building the request.
    /// </summary>
    public sealed class AddRequest
    {
        /// <summary>Gets the resolved element kind.</summary>
        public StencilKind Kind { get; init; }

        /// <summary>Gets the resolved stencil whose identity and preset defaults are stamped, or <see langword="null"/> for a bare primitive.</summary>
        public StencilDto? Stencil { get; init; }

        /// <summary>Gets the display name; when omitted a stencil supplies its label.</summary>
        public string? Name { get; init; }

        /// <summary>Gets the target page (a 1-based index or a page name); when omitted the first page is used or created.</summary>
        public string? Page { get; init; }

        /// <summary>Gets the left coordinate; when omitted a non-overlapping position is chosen.</summary>
        public int? Left { get; init; }

        /// <summary>Gets the top coordinate; when omitted a non-overlapping position is chosen.</summary>
        public int? Top { get; init; }

        /// <summary>Gets the width; honored for trust boundaries and explicit sizing.</summary>
        public int? Width { get; init; }

        /// <summary>Gets the height; honored for trust boundaries and explicit sizing.</summary>
        public int? Height { get; init; }

        /// <summary>Gets the authoring alias to assign, giving the element a deterministic, stable id.</summary>
        public string? Alias { get; init; }

        /// <summary>Gets the trust-boundary reference (alias, name, or GUID) to place the element inside.</summary>
        public string? Boundary { get; init; }

        /// <summary>Gets the <c>KEY=VALUE</c> custom-property assignments to apply.</summary>
        public IReadOnlyList<string> Properties { get; init; } = Array.Empty<string>();

        /// <summary>Gets a value indicating whether to store unknown/invalid property values instead of rejecting them.</summary>
        public bool Force { get; init; }
    }
}
