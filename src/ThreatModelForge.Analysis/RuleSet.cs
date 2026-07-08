namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// A set of rules and configuration.
    /// </summary>
    public class RuleSet : IDisposable
    {
        private bool disposedValue;

        /// <summary>
        /// Gets the rules.
        /// </summary>
        public Collection<Rule> Rules { get; } = new Collection<Rule>();

        /// <summary>
        /// Loads a default rule set based on the rules defined in the given assemblies. Discovery is
        /// resilient to third-party input: an assembly whose types cannot be enumerated, an abstract
        /// <see cref="Rule"/> subclass, a rule without a public parameterless constructor, or a rule
        /// whose constructor throws is reported through <paramref name="diagnostics"/> and skipped
        /// rather than aborting the whole load.
        /// </summary>
        /// <param name="analysisAssemblies">The analysis assemblies.</param>
        /// <param name="diagnostics">An optional sink for non-fatal load warnings.</param>
        /// <returns>A new instance of the <see cref="RuleSet"/> class.</returns>
        public static RuleSet LoadDefault(
            IEnumerable<Assembly> analysisAssemblies,
            Action<string>? diagnostics = null)
        {
            if (analysisAssemblies == null)
            {
                throw new ArgumentNullException(nameof(analysisAssemblies));
            }

            RuleSet result = new RuleSet();
            foreach (Assembly asm in analysisAssemblies)
            {
                if (asm == null)
                {
                    continue;
                }

                Type[] types;
                try
                {
                    types = asm.GetExportedTypes();
                }
                catch (Exception ex) when (ex is ReflectionTypeLoadException || ex is FileNotFoundException || ex is FileLoadException || ex is TypeLoadException)
                {
                    diagnostics?.Invoke($"Skipped rule assembly '{asm.GetName().Name}': {ex.Message}");
                    continue;
                }

                foreach (Type t in types)
                {
                    if (!typeof(Rule).IsAssignableFrom(t) || t.IsAbstract)
                    {
                        continue;
                    }

                    if (t.GetConstructor(Type.EmptyTypes) == null)
                    {
                        diagnostics?.Invoke($"Skipped rule '{t.FullName}': no public parameterless constructor.");
                        continue;
                    }

                    try
                    {
                        Rule r = (Rule)Activator.CreateInstance(t);
                        result.Rules.Add(r);
                    }
                    catch (Exception ex) when (ex is MissingMethodException || ex is MemberAccessException || ex is TargetInvocationException)
                    {
                        diagnostics?.Invoke($"Skipped rule '{t.FullName}': {ex.Message}");
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Loads a rule set from a .ruleset file.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <param name="analysisAssemblies">The assemblies used for analysis.</param>
        /// <returns>
        /// A new instance of the <see cref="RuleSet"/> class configured based on the file.
        /// </returns>
        /// <remarks>
        /// In FxCop you can define the same rule in multiple places and choose an implementation.
        /// In our case, one rule is only defined in one place, so we can key off the rule id.
        /// </remarks>
        public static RuleSet Load(
            string path,
            IEnumerable<Assembly> analysisAssemblies)
        {
            RuleSet result = LoadDefault(analysisAssemblies);
            result.ApplyOverrides(path);
            return result;
        }

        /// <summary>
        /// Applies the severity and disable overrides from a <c>.ruleset</c> file to the rules in
        /// this set. Rule ids listed in the file that are not present in the set are ignored, so the
        /// same override file composes over any rule source (built-in or custom). Matching is
        /// case-insensitive.
        /// </summary>
        /// <param name="path">The path to the <c>.ruleset</c> file.</param>
        public void ApplyOverrides(string path)
        {
            Xml.RuleSetXml xml = Xml.RuleSetXml.Load(path);

            foreach (Xml.RulesXml ruleBlock in xml.Rules)
            {
                foreach (Xml.RuleXml ruleOverride in ruleBlock.Rules)
                {
                    Rule rule = this.Rules.FirstOrDefault(
                        e => string.Equals(e.ID, ruleOverride.Id, StringComparison.OrdinalIgnoreCase));
                    if (rule == null)
                    {
                        continue;
                    }

                    switch (ruleOverride.Action)
                    {
                        case Xml.RuleAction.Error:
                            rule.Severity = MessageSeverity.Error;
                            break;
                        case Xml.RuleAction.Warning:
                            rule.Severity = MessageSeverity.Warning;
                            break;
                        case Xml.RuleAction.Info:
                            rule.Severity = MessageSeverity.Info;
                            break;
                        default:
                            rule.Disabled = true;
                            break;
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Evaluates all the rules in the rule set.
        /// </summary>
        /// <param name="context">The evaluation context.</param>
        public void Evaluate(RuleEvaluationContext context)
        {
            foreach (Rule rule in this.Rules)
            {
                if (!rule.Disabled)
                {
                    rule.Evaluate(context);
                }
            }
        }

        /// <summary>
        /// Disables every rule that belongs to one of the given packs or whose id is listed, so a
        /// subsequent call to <see cref="Evaluate"/> skips it. Matching is case-insensitive.
        /// </summary>
        /// <param name="disabledPacks">The rule pack ids to disable; may be <see langword="null"/>.</param>
        /// <param name="disabledRuleIds">The individual rule ids to disable; may be <see langword="null"/>.</param>
        public void Disable(IEnumerable<string>? disabledPacks, IEnumerable<string>? disabledRuleIds)
        {
            HashSet<string> packs = new HashSet<string>(
                disabledPacks ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            HashSet<string> ids = new HashSet<string>(
                disabledRuleIds ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            if (packs.Count == 0 && ids.Count == 0)
            {
                return;
            }

            foreach (Rule rule in this.Rules)
            {
                if (packs.Contains(rule.Pack) || ids.Contains(rule.ID))
                {
                    rule.Disabled = true;
                }
            }
        }

        /// <summary>
        /// Disposes the rules.
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to dispose both managed and unmanaged resources;
        /// <c>false</c> to only dispose unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    foreach (Rule rule in this.Rules)
                    {
                        rule.Dispose();
                    }

                    this.Rules.Clear();
                }

                this.disposedValue = true;
            }
        }
    }
}
