namespace ThreatModelForge.Engine
{
    using System.Collections.Generic;

    /// <summary>
    /// The result of a three-way merge: the merged model (always a valid model, never marker-laced)
    /// and any conflicts that were resolved in favor of <c>ours</c>.
    /// </summary>
    public sealed class MergeResultDto
    {
        /// <summary>Gets the merged model.</summary>
        public TmForgeModelDto? Merged { get; init; }

        /// <summary>Gets the conflicts the merge could not reconcile automatically; empty for a clean merge.</summary>
        public IReadOnlyList<MergeConflictDto>? Conflicts { get; init; }
    }
}
