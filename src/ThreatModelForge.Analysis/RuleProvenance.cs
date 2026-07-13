namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Preserves the source identity and expressions from which a declarative rule was compiled.
    /// </summary>
    public sealed class RuleProvenance
    {
        /// <summary>Initializes a new instance of the <see cref="RuleProvenance"/> class.</summary>
        /// <param name="sourceId">The source-local rule identifier.</param>
        /// <param name="categoryId">The source-local category identifier.</param>
        /// <param name="location">The optional source location.</param>
        /// <param name="expressions">The original source expressions.</param>
        internal RuleProvenance(
            string? sourceId,
            string? categoryId,
            string? location,
            IReadOnlyList<RuleSourceExpression> expressions)
        {
            this.SourceId = sourceId;
            this.CategoryId = categoryId;
            this.Location = location;
            this.Expressions = expressions;
        }

        /// <summary>Gets the source-local rule identifier.</summary>
        public string? SourceId { get; }

        /// <summary>Gets the source-local category identifier.</summary>
        public string? CategoryId { get; }

        /// <summary>Gets the source location, when available.</summary>
        public string? Location { get; }

        /// <summary>Gets the preserved source expressions.</summary>
        public IReadOnlyList<RuleSourceExpression> Expressions { get; }
    }
}
