namespace ThreatModelForge.Formats
{
    using System.Collections.Generic;

    /// <summary>
    /// A single diagram element in a <see cref="TmForgeJsonModel"/>.
    /// </summary>
    public sealed class TmForgeJsonElement
    {
        /// <summary>Gets the client-assigned element identifier.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the element kind: <c>process</c>, <c>datastore</c>, <c>external</c>, or <c>boundary</c>.</summary>
        public string Kind { get; init; } = "process";

        /// <summary>Gets the display name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets the x coordinate of the element's top-left corner.</summary>
        public int X { get; init; }

        /// <summary>Gets the y coordinate of the element's top-left corner.</summary>
        public int Y { get; init; }

        /// <summary>Gets the width, when the element is explicitly sized (for example, trust boundaries).</summary>
        public int? Width { get; init; }

        /// <summary>Gets the height, when the element is explicitly sized (for example, trust boundaries).</summary>
        public int? Height { get; init; }

        /// <summary>Gets the engine custom properties (for example, <c>DataType</c>) attached to the element.</summary>
        public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
    }
}
