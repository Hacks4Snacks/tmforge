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
        private readonly List<RuleValueBinding> bindings = new List<RuleValueBinding>();

        /// <summary>Gets the shared empty policy for properties no rule reads.</summary>
        public static PropertyPolicy Empty { get; } = new PropertyPolicy();

        /// <summary>Gets the rules that read the property (id and severity), in first-seen order.</summary>
        public IReadOnlyList<(string Id, string Severity)> Rules => this.rules;

        /// <summary>Gets the flagged values across those rules, in first-seen order.</summary>
        public IReadOnlyList<string> Flagged => this.flagged;

        /// <summary>
        /// Gets the per-rule bindings that read the property, each carrying the rule id, its severity,
        /// and the specific values that rule flags. Unlike <see cref="Rules"/> and <see cref="Flagged"/>,
        /// which are flattened, this preserves which rule flags which value so an author can predict the
        /// exact rule a value triggers.
        /// </summary>
        public IReadOnlyList<RuleValueBinding> Bindings => this.bindings;

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
            foreach (string value in values.Where(value => !this.flagged.Contains(value, StringComparer.OrdinalIgnoreCase)))
            {
                this.flagged.Add(value);
            }
        }

        /// <summary>Records a single rule's binding to this property, preserving which values it flags.</summary>
        /// <param name="id">The rule id.</param>
        /// <param name="severity">The rule severity.</param>
        /// <param name="flaggedValues">The values this rule flags (empty when it fires on absence or a computed condition).</param>
        public void AddBinding(string id, string severity, IReadOnlyList<string> flaggedValues)
        {
            this.bindings.Add(new RuleValueBinding(id, severity, flaggedValues));
        }

        /// <summary>A single rule's binding to a property: its id, severity, and the values it flags.</summary>
        internal sealed class RuleValueBinding
        {
            /// <summary>Initializes a new instance of the <see cref="RuleValueBinding"/> class.</summary>
            /// <param name="id">The rule id.</param>
            /// <param name="severity">The rule severity.</param>
            /// <param name="flagged">The values the rule flags (empty when it fires on absence or a condition).</param>
            public RuleValueBinding(string id, string severity, IReadOnlyList<string> flagged)
            {
                this.Id = id;
                this.Severity = severity;
                this.Flagged = flagged;
            }

            /// <summary>Gets the rule id.</summary>
            public string Id { get; }

            /// <summary>Gets the rule severity.</summary>
            public string Severity { get; }

            /// <summary>Gets the values the rule flags (empty when it fires on absence or a condition).</summary>
            public IReadOnlyList<string> Flagged { get; }
        }
    }
}
