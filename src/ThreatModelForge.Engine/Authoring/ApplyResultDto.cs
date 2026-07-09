namespace ThreatModelForge.Engine
{
    /// <summary>
    /// The result of applying a declarative manifest on the <see cref="AuthoringService"/> facade: the
    /// built model and the counts of what was created on success, or a blocking error.
    /// </summary>
    public sealed class ApplyResultDto
    {
        /// <summary>Gets a value indicating whether the manifest was applied.</summary>
        public bool Success { get; init; }

        /// <summary>Gets the blocking error message when <see cref="Success"/> is <see langword="false"/>.</summary>
        public string? Error { get; init; }

        /// <summary>Gets the built model on success; otherwise <see langword="null"/>.</summary>
        public TmForgeModelDto? Model { get; init; }

        /// <summary>Gets the number of trust boundaries created.</summary>
        public int Boundaries { get; init; }

        /// <summary>Gets the number of elements created.</summary>
        public int Elements { get; init; }

        /// <summary>Gets the number of data flows created.</summary>
        public int Flows { get; init; }
    }
}
