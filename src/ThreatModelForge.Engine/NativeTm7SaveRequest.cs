namespace ThreatModelForge.Engine
{
    /// <summary>A retained native TM7 source and its edited canvas projection.</summary>
    public sealed class NativeTm7SaveRequest
    {
        /// <summary>Gets the original TM7 bytes, encoded as base64.</summary>
        public string ContentBase64 { get; init; } = string.Empty;

        /// <summary>Gets the edited canvas projection to apply to the source.</summary>
        public TmForgeModelDto? Model { get; init; }
    }
}
