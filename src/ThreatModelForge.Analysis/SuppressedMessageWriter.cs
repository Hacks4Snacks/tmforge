namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.ObjectModel;

    /// <summary>
    /// A message writer that filters for suppressed messages.
    /// </summary>
    internal class SuppressedMessageWriter : MessageWriter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SuppressedMessageWriter"/> class.
        /// </summary>
        /// <param name="inner">The inner writer.</param>
        public SuppressedMessageWriter(MessageWriter inner)
        {
            this.Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        /// <summary>
        /// Gets the suppressions.
        /// </summary>
        public Collection<SuppressMessage> Suppressions { get; } = new Collection<SuppressMessage>();

        /// <summary>
        /// Gets the inner writer.
        /// </summary>
        public MessageWriter Inner { get; }

        /// <summary>
        /// Gets the set of messages written to the inner writer.
        /// </summary>
        public Collection<Message> Messages { get; } = new Collection<Message>();

        /// <summary>
        /// Gets the set of messages that were suppressed.
        /// </summary>
        public Collection<Message> SuppressedMessages { get; } = new Collection<Message>();

        /// <inheritdoc/>
        public override void Write(Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (!this.IsSuppressed(message))
            {
                this.Messages.Add(message);
                this.Inner.Write(message);
            }
            else
            {
                this.SuppressedMessages.Add(message);
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// These messages does not participate in suppression.
        /// </remarks>
        public override void WriteCore(
            MessageSeverity severity,
            string messageID,
            string text)
        {
            this.Inner.WriteCore(severity, messageID, text);
        }

        private bool IsSuppressed(Message message)
        {
            foreach (SuppressMessage suppression in this.Suppressions)
            {
                if (suppression.IsMatch(message))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
