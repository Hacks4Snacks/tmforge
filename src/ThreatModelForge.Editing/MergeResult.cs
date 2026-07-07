namespace ThreatModelForge.Editing
{
    using System.Collections.Generic;
    using ThreatModelForge.Model;

    /// <summary>
    /// The outcome of a three-way merge: the merged model (the mutated <c>ours</c> model) and any
    /// conflicts that were resolved in favor of <c>ours</c>.
    /// </summary>
    public sealed class MergeResult
    {
        /// <summary>Gets the merged model. It is always a valid model, never one with textual conflict markers.</summary>
        public ThreatModel Merged { get; init; } = null!;

        /// <summary>Gets the conflicts the merge could not reconcile automatically; empty for a clean merge.</summary>
        public IReadOnlyList<MergeConflict> Conflicts { get; init; } = new List<MergeConflict>();

        /// <summary>Gets a value indicating whether the merge was clean (no conflicts).</summary>
        public bool IsClean => this.Conflicts.Count == 0;
    }
}
