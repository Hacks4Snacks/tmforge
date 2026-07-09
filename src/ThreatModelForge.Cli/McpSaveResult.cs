namespace ThreatModelForge.Cli
{
    /// <summary>
    /// The result of the <c>tmforge mcp</c> <c>save</c> tool: where the model was written, in what
    /// format, and how many bytes.
    /// </summary>
    public sealed class McpSaveResult
    {
        /// <summary>Gets the path the model was written to.</summary>
        public string Path { get; init; } = string.Empty;

        /// <summary>Gets the format id the model was written in.</summary>
        public string Format { get; init; } = string.Empty;

        /// <summary>Gets the number of bytes written.</summary>
        public int Bytes { get; init; }
    }
}
