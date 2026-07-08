namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using ThreatModelForge.Analysis;

    /// <summary>
    /// Joins the typed property schema with the rule set. For each <c>(base primitive, property)</c>
    /// it collects the rules that read it — declared by each rule's <see cref="Rule.PropertyBindings"/>
    /// — with their severities, and the union of the values those rules flag. The rules are the single
    /// source of truth, so a rule's severity and flagged values are never duplicated in the schema and
    /// cannot drift from the code that reads the property.
    /// </summary>
    internal sealed class PropertyPolicyIndex
    {
        private readonly Dictionary<string, PropertyPolicy> byKey;

        private PropertyPolicyIndex(Dictionary<string, PropertyPolicy> byKey)
        {
            this.byKey = byKey;
        }

        /// <summary>Builds the index by reflecting each rule's property bindings.</summary>
        /// <param name="ruleAssemblies">The assemblies that contain the rules.</param>
        /// <returns>A populated index.</returns>
        public static PropertyPolicyIndex Build(IEnumerable<Assembly> ruleAssemblies)
        {
            Dictionary<string, PropertyPolicy> map = new Dictionary<string, PropertyPolicy>(StringComparer.OrdinalIgnoreCase);
            using RuleSet ruleSet = RuleSet.LoadDefault(ruleAssemblies);
            foreach (Rule rule in ruleSet.Rules)
            {
                string severity = rule.Severity.ToString();
                foreach (PropertyBinding binding in rule.PropertyBindings)
                {
                    string key = Key(binding.AppliesTo, binding.PropertyName);
                    if (!map.TryGetValue(key, out PropertyPolicy? policy))
                    {
                        policy = new PropertyPolicy();
                        map[key] = policy;
                    }

                    policy.AddRule(rule.ID, severity);
                    policy.AddFlagged(binding.FlaggedValues);
                    policy.AddBinding(rule.ID, severity, binding.FlaggedValues);
                }
            }

            return new PropertyPolicyIndex(map);
        }

        /// <summary>Gets the policy for a property, or an empty policy when no rule reads it.</summary>
        /// <param name="appliesTo">The DFD primitive.</param>
        /// <param name="name">The property name.</param>
        /// <returns>The matching policy, or <see cref="PropertyPolicy.Empty"/>.</returns>
        public PropertyPolicy For(string appliesTo, string name)
        {
            return this.byKey.TryGetValue(Key(appliesTo, name), out PropertyPolicy? policy) ? policy : PropertyPolicy.Empty;
        }

        private static string Key(string appliesTo, string name)
        {
            return appliesTo + "\u0000" + name;
        }
    }
}
