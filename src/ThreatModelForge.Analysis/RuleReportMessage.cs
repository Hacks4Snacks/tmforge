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
        /// Gets or sets the stable identifier of the target entity (if any).
        /// </summary>
        /// <remarks>
        /// <see cref="Entity"/> holds the target's display text, which is what a reader wants to see
        /// but is the wrong thing to reconcile on: it changes when an element is renamed, and two
        /// elements are allowed to share a name. The guid is what actually identifies the element in
        /// the persisted model, so it is what lets a consumer join a report back to the model or match
        /// the same finding across two runs.
        /// </remarks>
        public Guid? TargetId { get; set; }

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
                TargetId = message.Target?.Guid,
                Text = message.Text,
                Entity = message.Target?.DisplayText(),
            };
        }
    }
}
