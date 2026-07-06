namespace ThreatModelForge.Analysis.Tests
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Mock message writer implementation.
    /// </summary>
    internal class MockMessageWriter : MessageWriter
    {
        /// <summary>
        /// Gets the messages.
        /// </summary>
        public List<Message> Messages { get; } = new List<Message>();

        /// <summary>
        /// Gets the core messages.
        /// </summary>
        public List<Tuple<MessageSeverity, string, string>> CoreMessages { get; } =
            new List<Tuple<MessageSeverity, string, string>>();

        /// <inheritdoc/>
        public override void Write(Message message)
        {
            this.Messages.Add(message);
        }

        /// <inheritdoc/>
        public override void WriteCore(
            MessageSeverity severity,
            string messageID,
            string text)
        {
            this.CoreMessages.Add(
                new Tuple<MessageSeverity, string, string>(severity, messageID, text));
        }
    }
}
