namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Declares a typed custom property that a rule reads, and (optionally) the values of that
    /// property the rule flags as risky. A rule owns the policy attached to a property, so an
    /// authoring surface can join the property schema with the rule set and tell an author which
    /// rule consumes a property — and which values to avoid — without a separate lint pass. Because
    /// the binding lives on the rule, the rule's severity is never duplicated and the link cannot
    /// drift from the code that reads the property.
    /// </summary>
    public sealed class PropertyBinding
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyBinding"/> class.
        /// </summary>
        /// <param name="appliesTo">The DFD primitive the property belongs to (<c>process</c>, <c>datastore</c>, <c>external</c>, or <c>flow</c>).</param>
        /// <param name="propertyName">The custom-property key the rule reads (for example <c>Algorithm</c>).</param>
        /// <param name="flaggedValues">The values of the property this rule flags as risky; empty when the rule fires on absence or a condition rather than a specific value.</param>
        public PropertyBinding(string appliesTo, string propertyName, params string[] flaggedValues)
        {
            this.AppliesTo = appliesTo ?? throw new ArgumentNullException(nameof(appliesTo));
            this.PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            this.FlaggedValues = flaggedValues ?? Array.Empty<string>();
        }

        /// <summary>Gets the DFD primitive the property belongs to.</summary>
        public string AppliesTo { get; }

        /// <summary>Gets the custom-property key the rule reads.</summary>
        public string PropertyName { get; }

        /// <summary>Gets the values of the property this rule flags as risky (empty when none).</summary>
        public IReadOnlyList<string> FlaggedValues { get; }
    }
}
