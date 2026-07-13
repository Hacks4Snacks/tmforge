namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.ObjectModel;
    using System.IO;

    /// <summary>
    /// A message writer that filters for suppressed messages.
    /// </summary>
    internal class SuppressedMessageWriter : MessageWriter
    {
        private const int MaxMessages = 100000;
        private const long MaxMessageCharacters = 64L * 1024 * 1024;

        private int messageCount;
        private long messageCharacters;

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

            this.AccountForOutput(message.Text);

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
            this.AccountForOutput(text);
            this.Inner.WriteCore(severity, messageID, text);
        }

        private void AccountForOutput(string? text)
        {
            this.messageCount++;
            this.messageCharacters += text?.Length ?? 0;
            if (this.messageCount > MaxMessages)
            {
                throw new InvalidDataException($"Analysis exceeded the message limit of {MaxMessages}.");
            }

            if (this.messageCharacters > MaxMessageCharacters)
            {
                throw new InvalidDataException(
                    $"Analysis exceeded the message-output limit of {MaxMessageCharacters} characters.");
            }
        }

        private bool IsSuppressed(Message message)
        {
            foreach (SuppressMessage suppression in this.Suppressions.Where(suppression => suppression.IsMatch(message)))
            {
                return true;
            }

            return false;
        }
    }
}
