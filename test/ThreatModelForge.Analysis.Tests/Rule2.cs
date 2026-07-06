namespace ThreatModelForge.Analysis.Tests
{
    using System;

    /// <summary>
    /// Sample test rule 2.
    /// </summary>
    public class Rule2 : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Rule2"/> class.
        /// </summary>
        public Rule2()
            : base(1235, MessageSeverity.Warning, "Test")
        {
        }

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
                Text = "Rule2",
                Source = this,
            };

            context.Writer.Write(message);
        }
    }
}
