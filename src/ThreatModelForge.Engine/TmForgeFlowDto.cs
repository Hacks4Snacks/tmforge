namespace ThreatModelForge.Engine
{
    using System.Collections.Generic;

    /// <summary>
    /// A data flow between two elements.
    /// </summary>
    public sealed class TmForgeFlowDto
    {
        /// <summary>Gets the client-assigned flow identifier.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the source element identifier.</summary>
        public string Source { get; init; } = string.Empty;

        /// <summary>Gets the target element identifier.</summary>
        public string Target { get; init; } = string.Empty;

        /// <summary>Gets the flow label.</summary>
        public string? Name { get; init; }

        /// <summary>Gets the engine custom properties (for example, <c>Protocol</c>, <c>DataType</c>) attached to the flow.</summary>
        public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
    }
}
