namespace ThreatModelForge.Formats
{
    /// <summary>
    /// A triage-overlay entry carried on <c>tmforge-json</c>: the persisted lifecycle state of one
    /// generated threat, so risk acceptance round-trips with the structural model even though the
    /// full (regenerable) threat register is not stored. Keyed by the threat's register id
    /// (<c>{targetGuid:N}:{ruleId}</c>). Only threats whose state differs from the default
    /// (<c>Open</c>) are recorded.
    /// </summary>
    public sealed class TmForgeJsonThreatState
    {
        /// <summary>Gets the register id of the threat this state applies to.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the triage state (<c>Open</c> or <c>Accepted</c>).</summary>
        public string State { get; init; } = "Open";

        /// <summary>Gets the risk-acceptance justification, when the threat has been accepted.</summary>
        public string? Justification { get; init; }
    }
}
