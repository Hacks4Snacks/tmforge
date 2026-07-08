namespace ThreatModelForge.Analysis
{
    using System.Collections.Generic;

    /// <summary>
    /// The root of a declarative rule spec file (<c>*.tmrules.json</c>): a list of rule definitions
    /// authored as data rather than compiled code. Because a spec is inspectable data — not arbitrary
    /// code — it is the safe way to ship shared or third-party rule packs. Deserialized by
    /// <see cref="DeclarativeRuleProvider"/>.
    /// </summary>
    internal sealed class DeclarativeRuleFile
    {
        /// <summary>Gets or sets the rules declared in the file.</summary>
        public List<DeclarativeRuleSpec>? Rules { get; set; }
    }
}
