namespace ThreatModelForge.Formats
{
    using System.Collections.Generic;

    /// <summary>
    /// A directed data flow between two elements in a <see cref="TmForgeJsonModel"/>.
    /// </summary>
    public sealed class TmForgeJsonFlow
    {
        /// <summary>Gets the client-assigned flow identifier.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the source element identifier.</summary>
        public string Source { get; init; } = string.Empty;

        /// <summary>Gets the target element identifier.</summary>
        public string Target { get; init; } = string.Empty;

        /// <summary>Gets the display name / label.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets the engine custom properties (for example, <c>Protocol</c>, <c>DataType</c>) attached to the flow.</summary>
        public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
    }
}
