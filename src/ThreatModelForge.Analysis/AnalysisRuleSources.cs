namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// The single place that assembles a <see cref="RuleSet"/> from every rule source: the built-in
    /// first-party rules (always) plus any opt-in declarative rules. Every load site — the CLI, the HTTP
    /// API, the in-browser engine, threat generation, and the property policy — resolves rules through
    /// here, so a custom rule is seen everywhere or nowhere, and there is exactly one place that names
    /// the built-in rules assembly.
    /// </summary>
    public static class AnalysisRuleSources
    {
        private const string BuiltInRulesAssembly = "ThreatModelForge.Analysis.Rules";

        /// <summary>
        /// Gets the built-in first-party rule assemblies. This is the only place the built-in rules
        /// assembly is named; every load site resolves it here.
        /// </summary>
        /// <returns>The built-in rule assemblies.</returns>
        public static IReadOnlyList<Assembly> BuiltInAssemblies()
        {
            return new[] { Assembly.Load(BuiltInRulesAssembly) };
        }

        /// <summary>
        /// Creates a rule set containing the built-in rules plus any declarative rules selected by
        /// <paramref name="options"/>. A custom rule whose id collides with an already-loaded rule is
        /// reported through the options' diagnostics sink and dropped, so the built-in <c>TM####</c>
        /// namespace always wins and ids stay unique across SARIF, suppressions, and JSON output.
        /// </summary>
        /// <param name="options">The opt-in rule sources, or <see langword="null"/> for built-in rules only.</param>
        /// <returns>A new rule set. The caller owns and disposes it.</returns>
        public static RuleSet Create(RuleSourceOptions? options = null)
        {
            return Create(options, out _);
        }

        /// <summary>
        /// Creates a rule set as <see cref="Create(RuleSourceOptions)"/> does and reports the effective
        /// custom rule packs it loaded, so a host can show which pack content actually ran (id, version,
        /// and content fingerprint) instead of assuming a selection was honored.
        /// </summary>
        /// <param name="options">The opt-in rule sources, or <see langword="null"/> for built-in rules only.</param>
        /// <param name="packs">The custom rule packs that contributed rules to the returned set.</param>
        /// <returns>A new rule set. The caller owns and disposes it.</returns>
        public static RuleSet Create(RuleSourceOptions? options, out IReadOnlyList<RulePackDefinition> packs)
        {
            Action<string>? diagnostics = options?.Diagnostics;
            RuleSet ruleSet = RuleSet.LoadDefault(BuiltInAssemblies(), diagnostics);
            packs = Array.Empty<RulePackDefinition>();

            if (options == null || options.IsEmpty)
            {
                return ruleSet;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Rule existing in ruleSet.Rules)
            {
                ids.Add(existing.ID);
            }

            RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(options.SpecPaths, options.Contents, diagnostics);
            HashSet<string> loadedPackIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (Rule rule in bundle.Rules)
            {
                if (!ids.Add(rule.ID))
                {
                    diagnostics?.Invoke($"Skipped custom rule '{rule.ID}': a rule with that id is already loaded.");
                    rule.Dispose();
                    continue;
                }

                if (rule.PackDefinition != null)
                {
                    loadedPackIds.Add(rule.PackDefinition.Id);
                }

                ruleSet.Rules.Add(rule);
            }

            packs = bundle.Packs.Where(pack => loadedPackIds.Contains(pack.Id)).ToList();
            return ruleSet;
        }
    }
}
