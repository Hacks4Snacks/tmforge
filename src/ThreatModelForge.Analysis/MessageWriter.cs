namespace ThreatModelForge.Analysis
{
    using System;

    /// <summary>
    /// Abstract base class of consumers of message output written by instances of the <see cref="Rule"/> class.
    /// </summary>
    public abstract class MessageWriter
    {
        /// <summary>
        /// The string to print for a null value.
        /// </summary>
        /// <remarks>
        /// This should never be user visible, but should help with debugging output
        /// for rule developers.
        /// </remarks>
        private const string NullPrintString = "(null)";

        /// <summary>
        /// Gets or sets a value indicating whether the stream has errors.
        /// </summary>
        public bool HasErrors { get; protected set; }

        /// <summary>
        /// Writes a message.
        /// </summary>
        /// <param name="message">The message to write.</param>
        public virtual void Write(Message message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            // messages for rules at the file scope do not have a model.
            string coreText = message!.Model != null ?
                $"{message!.Model!.Header ?? NullPrintString}: {message!.Text ?? NullPrintString}" :
                message!.Text ?? NullPrintString;
            this.WriteCore(message.Severity, message!.Source?.ID ?? NullPrintString, coreText);
        }

        /// <summary>
        /// Writes a core message to the output that isn't bound to a rule.
        /// </summary>
        /// <param name="severity">The message severity.</param>
        /// <param name="messageID">The message ID, ex. TM1000.</param>
        /// <param name="text">The text message content.</param>
        public abstract void WriteCore(
            MessageSeverity severity,
            string messageID,
            string text);
    }
}
