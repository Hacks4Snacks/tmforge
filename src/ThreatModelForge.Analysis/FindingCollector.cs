namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A <see cref="MessageWriter"/> that captures the raw rule findings — each with its source rule
    /// and target element/flow — so the threat projection can key every finding to the element or flow
    /// it was raised against. This is how validation findings <em>inform</em> the threat register.
    /// </summary>
    internal sealed class FindingCollector : MessageWriter
    {
        private readonly List<Message> messages = new List<Message>();

        /// <summary>Gets the collected findings.</summary>
        public IReadOnlyList<Message> Messages => this.messages;

        /// <inheritdoc/>
        public override void Write(Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            this.messages.Add(message);
        }

        /// <inheritdoc/>
        public override void WriteCore(MessageSeverity severity, string messageID, string text)
        {
            // The projection reads whole Message objects via Write; the flat overload is unused.
        }
    }
}
