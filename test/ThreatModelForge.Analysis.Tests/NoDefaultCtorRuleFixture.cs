namespace ThreatModelForge.Analysis.Tests
{
    /// <summary>
    /// A test fixture: a concrete <see cref="Rule"/> with no public parameterless constructor. Rule
    /// discovery must skip it (reporting a diagnostic) rather than throw when it appears in a scanned
    /// assembly.
    /// </summary>
    public sealed class NoDefaultCtorRuleFixture : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NoDefaultCtorRuleFixture"/> class.
        /// </summary>
        /// <param name="tag">A required argument, so the type has no public parameterless constructor.</param>
        public NoDefaultCtorRuleFixture(string tag)
            : base("NOCTOR-1", MessageSeverity.Info, "Test")
        {
            _ = tag;
        }

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
        }
    }
}
