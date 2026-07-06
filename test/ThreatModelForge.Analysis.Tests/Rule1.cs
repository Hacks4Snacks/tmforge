namespace ThreatModelForge.Analysis.Tests
{
    using System;

    /// <summary>
    /// Sample test rule 1.
    /// </summary>
    public class Rule1 : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Rule1"/> class.
        /// </summary>
        public Rule1()
            : base(1234, MessageSeverity.Warning, "Test")
        {
        }

        /// <summary>
        /// Gets a value indicating whether the rule has been disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Message message = new Message
            {
                Severity = this.Severity,
                Text = "Rule1",
                Source = this,
            };

            context.Writer.Write(message);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            this.IsDisposed = true;
        }
    }
}
