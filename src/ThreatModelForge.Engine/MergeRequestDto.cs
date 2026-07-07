namespace ThreatModelForge.Engine
{
    /// <summary>
    /// A three-way merge request: two edited models (<see cref="Ours"/>, <see cref="Theirs"/>) and
    /// their common ancestor (<see cref="Base"/>), each in the canonical tmforge-json shape.
    /// </summary>
    public sealed class MergeRequestDto
    {
        /// <summary>Gets the common ancestor model.</summary>
        public TmForgeModelDto? Base { get; init; }

        /// <summary>Gets the local model.</summary>
        public TmForgeModelDto? Ours { get; init; }

        /// <summary>Gets the incoming model.</summary>
        public TmForgeModelDto? Theirs { get; init; }
    }
}
