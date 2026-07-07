namespace ThreatModelForge.Engine
{
    using System.Collections.Generic;

    /// <summary>
    /// A DFD element (process, data store, external entity, or trust boundary).
    /// </summary>
    public sealed class TmForgeElementDto
    {
        /// <summary>Gets the client-assigned element identifier.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the element kind (<c>process</c>, <c>datastore</c>, <c>external</c>, <c>boundary</c>).</summary>
        public string Kind { get; init; } = string.Empty;

        /// <summary>Gets the display name.</summary>
        public string? Name { get; init; }

        /// <summary>Gets the left coordinate.</summary>
        public int X { get; init; }

        /// <summary>Gets the top coordinate.</summary>
        public int Y { get; init; }

        /// <summary>Gets the width (trust boundaries only).</summary>
        public int? Width { get; init; }

        /// <summary>Gets the height (trust boundaries only).</summary>
        public int? Height { get; init; }

        /// <summary>Gets the engine custom properties (for example, <c>DataType</c>) attached to the element.</summary>
        public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
    }
}
