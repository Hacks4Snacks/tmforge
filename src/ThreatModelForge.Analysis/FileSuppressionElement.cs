namespace ThreatModelForge.Analysis
{
    using System.Collections.Generic;

    /// <summary>
    /// Declares suppressions for a single file.
    /// </summary>
    public class FileSuppressionElement
    {
        /// <summary>
        /// Gets or sets the relative (or absolute) path to the threat model document.
        /// </summary>
        public string? File { get; set; }

        /// <summary>
        /// Gets or sets the list of suppressions.
        /// </summary>
#pragma warning disable CA1819 // Properties should not return arrays. System.Text.Json does not support deserializing read only collection properties.
#pragma warning disable SA1011 // Closing square brackets should be spaced correctly
        public SuppressMessage[]? Suppressions { get; set; }
#pragma warning restore SA1011 // Closing square brackets should be spaced correctly
#pragma warning restore CA1819 // Properties should not return arrays
    }
}
