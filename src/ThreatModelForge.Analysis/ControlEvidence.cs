namespace ThreatModelForge.Analysis
{
    /// <summary>
    /// What a model actually asserts about a security control, as distinct from what a rule may infer.
    /// The distinction exists because "we checked and the control is missing" and "nobody recorded
    /// whether the control exists" are different statements, and conflating them either invents a
    /// finding or — far worse — invents assurance.
    /// </summary>
    public enum ControlEvidence
    {
        /// <summary>
        /// The model says nothing either way: the property is absent, blank, or explicitly
        /// <c>Unknown</c>. This is never treated as the control being in place.
        /// </summary>
        Unevidenced,

        /// <summary>The model explicitly states the control is not in place (for example <c>No</c> or <c>None</c>).</summary>
        Absent,

        /// <summary>The model states the control is in place.</summary>
        Present,
    }
}
