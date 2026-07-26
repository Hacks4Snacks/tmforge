namespace ThreatModelForge.Engine
{
    using System;

    /// <summary>
    /// Selects which projections one analysis action materializes. The rule set is evaluated once
    /// regardless; this only decides what is derived from the collected messages, so a caller that
    /// wants findings does not pay for a threat register it will not read, and a caller that wants
    /// both does not evaluate the rules twice.
    /// </summary>
    [Flags]
    internal enum AnalysisProjection
    {
        /// <summary>Materialize nothing (used only as the empty state).</summary>
        None = 0,

        /// <summary>Materialize the transient findings.</summary>
        Findings = 1,

        /// <summary>Materialize the lifecycle-bearing threats, with triage and manual threats applied.</summary>
        Threats = 2,

        /// <summary>
        /// Materialize the persistable analysis evidence: every finding with its structural
        /// disposition and, when threat-bearing, the register id it projects to.
        /// </summary>
        Evidence = 4,

        /// <summary>
        /// Materialize the threat register split by origin and by standing against this run, so a
        /// stored entry the rules no longer produce is distinguishable from a live one.
        /// </summary>
        Register = 8,
    }
}
