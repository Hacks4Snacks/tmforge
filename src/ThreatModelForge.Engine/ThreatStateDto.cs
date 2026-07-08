namespace ThreatModelForge.Engine
{
    /// <summary>
    /// A triage-overlay entry: the persisted lifecycle state of one generated threat, carried on the
    /// model so risk acceptance round-trips with it. Keyed by the threat's register id
    /// (<c>{targetGuid:N}:{ruleId}</c>). Only threats whose state differs from the default
    /// (<c>Open</c>) need an entry.
    /// </summary>
    public sealed class ThreatStateDto
    {
        /// <summary>Gets the register id of the threat this state applies to.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the triage state (<c>Open</c> or <c>Accepted</c>).</summary>
        public string State { get; init; } = "Open";

        /// <summary>Gets the risk-acceptance justification, when the threat has been accepted.</summary>
        public string? Justification { get; init; }
    }
}
