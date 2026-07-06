namespace ThreatModelForge.Analysis
{
    using System;

    /// <summary>
    /// Generic properties about any tool that can be used as information in the <see cref="ModelReport"/> class.
    /// </summary>
    /// <seealso cref="ModelReport"/>
    public class ToolInfo
    {
        /// <summary>
        /// Gets or sets a simple name of the tool.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the full name of the tool.
        /// </summary>
        public string? FullName { get; set; }

        /// <summary>
        /// Gets or sets the name of the organization that produces the tool.
        /// </summary>
        public string? Organization { get; set; }

        /// <summary>
        /// Gets or sets the version of the tool.
        /// </summary>
        public string? Version { get; set; }

        /// <summary>
        /// Gets or sets a URL for more information about the tool.
        /// </summary>
        public Uri? InformationUri { get; set; }
    }
}
