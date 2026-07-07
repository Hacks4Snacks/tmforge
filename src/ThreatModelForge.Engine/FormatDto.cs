namespace ThreatModelForge.Engine
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Describes a registered file-format provider.
    /// </summary>
    public sealed class FormatDto
    {
        /// <summary>Gets the stable format identifier (for example, <c>tm7</c>).</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the human-readable name.</summary>
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>Gets the associated file extensions.</summary>
        public IReadOnlyList<string> Extensions { get; init; } = Array.Empty<string>();

        /// <summary>Gets a value indicating whether the provider can read the format.</summary>
        public bool CanRead { get; init; }

        /// <summary>Gets a value indicating whether the provider can write the format.</summary>
        public bool CanWrite { get; init; }
    }
}
