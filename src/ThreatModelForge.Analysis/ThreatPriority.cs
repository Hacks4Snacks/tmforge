namespace ThreatModelForge.Analysis
{
    /// <summary>
    /// The default priority of a generated threat. This is independent of finding severity: imported
    /// rule packs can preserve source priority while analysis continues to gate on severity.
    /// </summary>
    public enum ThreatPriority
    {
        /// <summary>The threat should be addressed with high priority.</summary>
        High,

        /// <summary>The threat should be addressed with medium priority.</summary>
        Medium,

        /// <summary>The threat should be addressed with low priority.</summary>
        Low,
    }
}
