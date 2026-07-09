namespace ThreatModelForge.Engine
{
    /// <summary>
    /// The inputs for <see cref="AuthoringService.RemoveThreat"/>: the id of the author-overlay threat
    /// entry to remove.
    /// </summary>
    public sealed class RemoveThreatRequest
    {
        /// <summary>Gets the threat id to remove.</summary>
        public string Id { get; init; } = string.Empty;
    }
}
