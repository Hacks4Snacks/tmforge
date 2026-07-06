namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System.Collections.Generic;

    /// <summary>
    /// Mock message writer used by tests.
    /// </summary>
    internal class MockMessageWriter : MessageWriter
    {
        /// <summary>
        /// Gets the messages.
        /// </summary>
        public IList<Message> Messages { get; } = new List<Message>();

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
            throw new System.NotImplementedException();
        }
    }
}
