namespace ThreatModelForge.Api
{
    using ThreatModelForge.Analysis;

    /// <summary>
    /// A no-op <see cref="MessageWriter"/>. Findings are read from the generated
    /// <see cref="ModelReport"/> after evaluation, so nothing needs to be written here.
    /// </summary>
    internal sealed class CollectingMessageWriter : MessageWriter
    {
        /// <inheritdoc/>
        public override void WriteCore(MessageSeverity severity, string messageID, string text)
        {
        }
    }
}
