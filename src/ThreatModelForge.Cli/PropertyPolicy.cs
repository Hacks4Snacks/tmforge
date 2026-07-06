namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The rules that read a single property and the values they flag as risky, accumulated from the
    /// rules' declared property bindings by <see cref="PropertyPolicyIndex"/>.
    /// </summary>
    internal sealed class PropertyPolicy
    {
        private readonly List<(string Id, string Severity)> rules = new List<(string Id, string Severity)>();
        private readonly List<string> flagged = new List<string>();

        /// <summary>Gets the shared empty policy for properties no rule reads.</summary>
        public static PropertyPolicy Empty { get; } = new PropertyPolicy();

        /// <summary>Gets the rules that read the property (id and severity), in first-seen order.</summary>
        public IReadOnlyList<(string Id, string Severity)> Rules => this.rules;

        /// <summary>Gets the flagged values across those rules, in first-seen order.</summary>
        public IReadOnlyList<string> Flagged => this.flagged;

        /// <summary>Adds a rule reference if it is not already present.</summary>
        /// <param name="id">The rule id.</param>
        /// <param name="severity">The rule severity.</param>
        public void AddRule(string id, string severity)
        {
            if (!this.rules.Any(rule => string.Equals(rule.Id, id, StringComparison.OrdinalIgnoreCase)))
            {
                this.rules.Add((id, severity));
            }
        }

        /// <summary>Adds flagged values that are not already present (case-insensitive).</summary>
        /// <param name="values">The values to add.</param>
        public void AddFlagged(IEnumerable<string> values)
        {
            foreach (string value in values)
            {
                if (!this.flagged.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    this.flagged.Add(value);
                }
            }
        }
    }
}
