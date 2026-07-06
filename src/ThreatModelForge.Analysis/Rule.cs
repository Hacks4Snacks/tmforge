namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Base class of all rules that can be run against a model.
    /// </summary>
    public abstract class Rule : IDisposable
    {
        private readonly int ruleId;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rule"/> class.
        /// </summary>
        /// <param name="id">The unique id of the rule.</param>
        /// <param name="defaultSeverity">The default severity for the rule.</param>
        /// <param name="pack">The identifier of the rule pack this rule belongs to.</param>
        protected Rule(int id, MessageSeverity defaultSeverity, string pack)
        {
            this.ruleId = id;
            this.Severity = defaultSeverity;
            this.Pack = pack ?? throw new ArgumentNullException(nameof(pack));
            this.FullDescription = string.Empty;
            this.HelpText = string.Empty;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="Rule"/> class.
        /// </summary>
        ~Rule()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(disposing: false);
        }

        /// <summary>
        /// Gets the rule id.
        /// </summary>
        public string ID
        {
            get
            {
                return $"TM{this.ruleId}";
            }
        }

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
        /// property without a separate lint pass. Empty by default.
        /// </summary>
        public virtual IReadOnlyList<PropertyBinding> PropertyBindings => Array.Empty<PropertyBinding>();

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
    }
}
