namespace ThreatModelForge.Engine
{
    using System.Collections.Generic;
    using ThreatModelForge.Analysis;

    /// <summary>
    /// Collects unsuppressed rule messages so the engine can retain each finding's exact target
    /// identity instead of reconstructing it from display text.
    /// </summary>
    internal sealed class CollectingMessageWriter : MessageWriter
    {
        /// <summary>Gets the messages written by the rule set.</summary>
        public List<Message> Messages { get; } = new List<Message>();

        /// <inheritdoc/>
        public override void Write(Message message)
        {
            this.Messages.Add(message);
        }

        /// <inheritdoc/>
        public override void WriteCore(MessageSeverity severity, string messageID, string text)
        {
        }
    }
}
