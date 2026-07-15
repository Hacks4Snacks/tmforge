namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text;

    /// <summary>
    /// A report on a single model document.
    /// </summary>
    public class ModelReport
    {
        /// <summary>
        /// Gets or sets the source document file path.
        /// </summary>
        public string? SourcePath { get; set; }

        /// <summary>
        /// Gets or sets the threat model name.
        /// </summary>
        public string? ThreatModelName { get; set; }

        /// <summary>
        /// Gets or sets the owner.
        /// </summary>
        public string? Owner { get; set; }

        /// <summary>
        /// Gets or sets the reviewer.
        /// </summary>
        public string? Reviewer { get; set; }

        /// <summary>
        /// Gets or sets the contributors.
        /// </summary>
        public string? Contributors { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the assumptions.
        /// </summary>
        public string? Assumptions { get; set; }

        /// <summary>
        /// Gets or sets external dependencies.
        /// </summary>
        public string? ExternalDependencies { get; set; }

        /// <summary>
        /// Gets or sets the tool version used to edit the model.
        /// </summary>
        public string? ToolVersion { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not threat generation is enabled.
        /// </summary>
        public bool ThreatGenerationEnabled { get; set; }

        /// <summary>
        /// Gets or sets the knowledge base name.
        /// </summary>
        public string? KnowledgeBaseName { get; set; }

        /// <summary>
        /// Gets or sets the knowledge base version.
        /// </summary>
        public string? KnowledgeBaseVersion { get; set; }

        /// <summary>
        /// Gets or sets information about the analysis tool used to generate the report.
        /// </summary>
        public ToolInfo? AnalysisTool { get; set; }

        /// <summary>
        /// Gets the set of diagram summaries.
        /// </summary>
        public Collection<DiagramSummary> DiagramSummaries { get; } = new Collection<DiagramSummary>();

        /// <summary>
        /// Gets the reports for each rule.
        /// </summary>
        public Collection<RuleReport> RuleReports { get; } = new Collection<RuleReport>();

        /// <summary>Gets the effective threat category catalog used by this analysis run.</summary>
        public Collection<RuleThreatCategory> ThreatCategories { get; } = new Collection<RuleThreatCategory>();

        /// <summary>
        /// Gets custom properties that can be set by rules.
        /// </summary>
        public IDictionary<string, string> CustomProperties { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
