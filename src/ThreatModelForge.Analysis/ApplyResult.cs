namespace ThreatModelForge.Analysis
{
    /// <summary>
    /// A summary of an idempotent <see cref="ThreatGenerator.Apply"/> pass over a model's register.
    /// </summary>
    public sealed class ApplyResult
    {
        /// <summary>Gets the number of new threats added to the register.</summary>
        public int Added { get; init; }

        /// <summary>Gets the number of existing auto-generated threats refreshed in place.</summary>
        public int Updated { get; init; }

        /// <summary>Gets the number of human-triaged threats left untouched.</summary>
        public int Preserved { get; init; }
    }
}
