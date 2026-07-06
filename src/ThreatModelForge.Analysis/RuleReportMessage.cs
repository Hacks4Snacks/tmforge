namespace ThreatModelForge.Analysis
{
    using System;

    /// <summary>
    /// A message that is contained in a <see cref="RuleReport"/>.
    /// </summary>
    public class RuleReportMessage
    {
        /// <summary>
        /// Gets or sets the diagram reference (if any).
        /// </summary>
        public Guid? Diagram { get; set; }

        /// <summary>
        /// Gets or sets the display text of the entity (if any).
        /// </summary>
        public string? Entity { get; set; }

        /// <summary>
        /// Gets or sets the text.
        /// </summary>
        public string? Text { get; set; }

        /// <summary>
        /// Creates a report message from a message.
        /// </summary>
        /// <param name="message">The source message.</param>
        /// <returns>A new instance of the <see cref="RuleReportMessage"/> class.</returns>
        public static RuleReportMessage FromMessage(Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return new RuleReportMessage
            {
                Diagram = message.Model?.Guid,
                Text = message.Text,
                Entity = message.Target?.DisplayText(),
            };
        }
    }
}
