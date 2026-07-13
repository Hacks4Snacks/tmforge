namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Defines a source property, its runtime display name, aliases, values, and applicable types.
    /// </summary>
    public sealed class RulePropertyDefinition
    {
        /// <summary>Initializes a new instance of the <see cref="RulePropertyDefinition"/> class.</summary>
        /// <param name="name">The canonical runtime property name.</param>
        /// <param name="aliases">The source aliases.</param>
        /// <param name="allowedValues">The allowed values.</param>
        /// <param name="elementTypeIds">The applicable element-type identifiers.</param>
        internal RulePropertyDefinition(
            string name,
            IReadOnlyList<string> aliases,
            IReadOnlyList<string> allowedValues,
            IReadOnlyList<string> elementTypeIds)
        {
            this.Name = name;
            this.Aliases = aliases;
            this.AllowedValues = allowedValues;
            this.ElementTypeIds = elementTypeIds;
        }

        /// <summary>Gets the canonical runtime property name.</summary>
        public string Name { get; }

        /// <summary>Gets additional source names that resolve to this property.</summary>
        public IReadOnlyList<string> Aliases { get; }

        /// <summary>Gets the allowed property values, or an empty list for free text.</summary>
        public IReadOnlyList<string> AllowedValues { get; }

        /// <summary>Gets the element-type identifiers on which the property is defined.</summary>
        public IReadOnlyList<string> ElementTypeIds { get; }
    }
}
