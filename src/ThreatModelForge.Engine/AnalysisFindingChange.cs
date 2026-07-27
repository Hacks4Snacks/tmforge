namespace ThreatModelForge.Engine
{
    /// <summary>
    /// One finding that survived between two analyses but whose classification moved.
    /// </summary>
    /// <remarks>
    /// The finding is the same one — same rule, same target, same occurrence — so this is not a change
    /// to what was detected but to what was concluded about it. Accepting a risk and a rule's severity
    /// being reconfigured both land here, and both are worth a reviewer's attention for different
    /// reasons than an introduced finding is.
    /// </remarks>
    public sealed class AnalysisFindingChange
    {
        /// <summary>Gets the finding as the base document recorded it.</summary>
        public AnalysisFindingDto Before { get; init; } = new AnalysisFindingDto();

        /// <summary>Gets the finding as the head document records it.</summary>
        public AnalysisFindingDto After { get; init; } = new AnalysisFindingDto();
    }
}
