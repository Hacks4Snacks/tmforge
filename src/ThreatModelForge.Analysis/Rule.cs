namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Text;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Base class of all rules that can be run against a model.
    /// </summary>
    public abstract class Rule : IDisposable
    {
        private const int MaxExpandedMessageCharacters = 65536;

        private readonly string ruleId;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rule"/> class with a built-in numeric id,
        /// surfaced as <c>TM&lt;id&gt;</c>. This is the convention for first-party rules.
        /// </summary>
        /// <param name="id">The unique numeric id of the rule; surfaced as <c>TM{id}</c>.</param>
        /// <param name="defaultSeverity">The default severity for the rule.</param>
        /// <param name="pack">The identifier of the rule pack this rule belongs to.</param>
        protected Rule(int id, MessageSeverity defaultSeverity, string pack)
            : this($"TM{id}", defaultSeverity, pack)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rule"/> class with an explicit string id.
        /// Third-party rule packs use this to declare their own id namespace (for example
        /// <c>ACME001</c>) rather than the built-in <c>TM####</c> convention, so their ids do not
        /// collide with first-party ids in SARIF, suppressions, or JSON output.
        /// </summary>
        /// <param name="id">The unique id of the rule.</param>
        /// <param name="defaultSeverity">The default severity for the rule.</param>
        /// <param name="pack">The identifier of the rule pack this rule belongs to.</param>
        protected Rule(string id, MessageSeverity defaultSeverity, string pack)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(id));
            }

            this.ruleId = id;
            this.Severity = defaultSeverity;
            this.Pack = pack ?? throw new ArgumentNullException(nameof(pack));
            this.FullDescription = string.Empty;
            this.HelpText = string.Empty;
        }

        /// <summary>
        /// Gets the rule id.
        /// </summary>
        public string ID => this.ruleId;

        /// <summary>
        /// Gets the identifier of the rule pack this rule belongs to.
        /// </summary>
        public string Pack { get; }

        /// <summary>
        /// Gets or sets the severity to use for messages.
        /// </summary>
        public MessageSeverity Severity { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not this rule is disabled.
        /// </summary>
        /// <remarks>
        /// Disabled rules will still show up in the report output of the rule set.
        /// </remarks>
        public bool Disabled { get; set; }

        /// <summary>
        /// Gets or sets human readable text describing what the rule is evaluating and why.
        /// </summary>
        [Localizable(true)]
        public string FullDescription { get; protected set; }

        /// <summary>
        /// Gets or sets human readable text describing how to address problems identified by the rule.
        /// </summary>
        [Localizable(true)]
        public string HelpText { get; protected set; }

        /// <summary>
        /// Gets or sets the documentation URL.
        /// </summary>
        public Uri? HelpUri { get; protected set; }

        /// <summary>
        /// Gets the typed custom properties this rule reads, together with the values it flags as
        /// risky. A rule that reads a property overrides this so an authoring surface can join the
        /// property schema with the rule set and show which rule (and severity) consumes each
        /// property without a separate analysis pass. Empty by default.
        /// </summary>
        public virtual IReadOnlyList<PropertyBinding> PropertyBindings => Array.Empty<PropertyBinding>();

        /// <summary>
        /// Gets the STRIDE category the rule's finding represents, or <see langword="null"/> when the
        /// rule is a structural or naming hygiene check rather than a threat. Threat generation reads
        /// this to decide which findings become persisted threats, so a rule declares its own threat
        /// identity in one place and there is no separate map to drift from.
        /// </summary>
        public virtual StrideCategory? Stride => null;

        /// <summary>
        /// Gets the external catalog references (CWE / CAPEC / MITRE ATT&amp;CK) for the threat this
        /// rule detects. Declared together with <see cref="Stride"/> on a threat-bearing rule; empty by
        /// default. The mitigation is not duplicated here — it is the rule's <see cref="HelpText"/>.
        /// </summary>
        public virtual IReadOnlyList<ThreatReference> ThreatReferences => Array.Empty<ThreatReference>();

        /// <summary>
        /// Gets the validated versioned pack definition that supplied this rule, or
        /// <see langword="null"/> for built-in and legacy declarative rules.
        /// </summary>
        public virtual RulePackDefinition? PackDefinition => null;

        /// <summary>Gets the preserved source identity and expressions for an imported rule.</summary>
        public virtual RuleProvenance? Provenance => null;

        /// <inheritdoc/>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Evaluates the rule against a given model.
        /// </summary>
        /// <param name="context">The evaluation context.</param>
        public abstract void Evaluate(RuleEvaluationContext context);

        /// <summary>
        /// Gets the text to display to identify an entity in a message.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns>Human readable text that can uniquely identify an entity in a message.</returns>
        protected static string GetEntityDisplayText(Entity entity)
        {
            return entity.DisplayText();
        }

        /// <summary>Expands known message tokens without allowing unbounded output allocation.</summary>
        /// <param name="template">The message template.</param>
        /// <param name="resolveToken">Resolves known token names; unknown tokens return <see langword="null"/>.</param>
        /// <returns>The expanded message.</returns>
        protected static string ExpandMessageTemplate(string template, Func<string, string?> resolveToken)
        {
            _ = template ?? throw new ArgumentNullException(nameof(template));
            _ = resolveToken ?? throw new ArgumentNullException(nameof(resolveToken));

            StringBuilder result = new StringBuilder(Math.Min(template.Length, MaxExpandedMessageCharacters));
            int index = 0;
            while (index < template.Length)
            {
                int start = template.IndexOf('{', index);
                if (start < 0)
                {
                    AppendBounded(result, template.Substring(index));
                    break;
                }

                AppendBounded(result, template.Substring(index, start - index));
                int end = template.IndexOf('}', start + 1);
                if (end < 0)
                {
                    AppendBounded(result, template.Substring(start));
                    break;
                }

                string token = template.Substring(start + 1, end - start - 1);
                string? replacement = resolveToken(token);
                AppendBounded(result, replacement ?? template.Substring(start, end - start + 1));
                index = end + 1;
            }

            return result.ToString();
        }

        /// <summary>
        /// Creates a message scoped to the whole model document.
        /// </summary>
        /// <param name="text">The message text.</param>
        /// <returns>A new instance of the <see cref="Message"/> class.</returns>
        protected Message CreateMessage(string text)
        {
            return this.CreateMessage(null, text);
        }

        /// <summary>
        /// Creates a message scoped to a diagram in the model document.
        /// </summary>
        /// <param name="model">The diagram.</param>
        /// <param name="text">The messages text.</param>
        /// <returns>A new instance of the <see cref="Message"/> class.</returns>
        protected Message CreateMessage(
            DrawingSurfaceModel? model,
            string text)
        {
            return this.CreateMessage(null, model, text);
        }

        /// <summary>
        /// Creates a message scoped to a given entity on a given diagram.
        /// </summary>
        /// <param name="target">The target entity.</param>
        /// <param name="model">The diagram containing the target.</param>
        /// <param name="text">The text message.</param>
        /// <returns>A new instance of the <see cref="Message"/> class.</returns>
        protected Message CreateMessage(
            Entity? target,
            DrawingSurfaceModel? model,
            string text)
        {
            return new Message
            {
                Source = this,
                Severity = this.Severity,
                Text = text,
                Model = model,
                Target = target,
            };
        }

        /// <summary>
        /// Override in derived classes to provide an implementation for the dispose pattern.
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to dispose both managed and unmanaged resource;
        /// <c>false</c> to only dispose unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
        }

        private static void AppendBounded(StringBuilder result, string value)
        {
            if (result.Length > MaxExpandedMessageCharacters - value.Length)
            {
                throw new InvalidDataException(
                    $"Expanded message exceeds the limit of {MaxExpandedMessageCharacters} characters.");
            }

            result.Append(value);
        }
    }
}
