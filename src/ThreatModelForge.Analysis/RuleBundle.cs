namespace ThreatModelForge.Analysis
{
    using System.Collections.Generic;

    /// <summary>
    /// The result of loading declarative rule documents: compiled rules plus validated v2 pack
    /// metadata. The caller owns and disposes the returned rules.
    /// </summary>
    public sealed class RuleBundle
    {
        /// <summary>Initializes a new instance of the <see cref="RuleBundle"/> class.</summary>
        /// <param name="rules">The compiled rules.</param>
        /// <param name="packs">The validated version 2 pack definitions.</param>
        internal RuleBundle(IReadOnlyList<Rule> rules, IReadOnlyList<RulePackDefinition> packs)
        {
            this.Rules = rules;
            this.Packs = packs;
        }

        /// <summary>Gets the compiled rules in declaration order after validation.</summary>
        public IReadOnlyList<Rule> Rules { get; }

        /// <summary>Gets validated version 2 pack definitions.</summary>
        public IReadOnlyList<RulePackDefinition> Packs { get; }
    }
}
