namespace ThreatModelForge.Analysis.Tests
{
    /// <summary>
    /// A test fixture: an abstract <see cref="Rule"/> subclass. It cannot be instantiated, so rule
    /// discovery must skip it rather than throw when it appears in a scanned assembly.
    /// </summary>
    public abstract class AbstractRuleFixture : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AbstractRuleFixture"/> class.
        /// </summary>
        protected AbstractRuleFixture()
            : base(9001, MessageSeverity.Warning, "Test")
        {
        }
    }
}
