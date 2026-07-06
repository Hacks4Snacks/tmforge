namespace ThreatModelForge.Api
{
    /// <summary>
    /// A base64-encoded document payload for the engine read and detect operations.
    /// </summary>
    public sealed class FileContentDto
    {
        /// <summary>Gets the base64-encoded document bytes.</summary>
        public string ContentBase64 { get; init; } = string.Empty;

        /// <summary>Gets an optional explicit format id; when omitted the engine sniffs the content.</summary>
        public string? FormatId { get; init; }
    }
}
