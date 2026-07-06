namespace ThreatModelForge.Cli
{
    using ThreatModelForge.Analysis;

    /// <summary>
    /// A <see cref="MessageWriter"/> that discards all messages. Used to drive read-only analysis
    /// such as <see cref="RuleEvaluationContext.GenerateListing"/>, which produces no output.
    /// </summary>
    internal sealed class NullMessageWriter : MessageWriter
    {
        /// <inheritdoc />
        public override void WriteCore(MessageSeverity severity, string messageID, string text)
        {
            // Intentionally discards all messages.
        }
    }
}
