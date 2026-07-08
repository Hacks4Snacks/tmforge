namespace ThreatModelForge.Analysis
{
    using System.Collections.Generic;

    /// <summary>
    /// A single declarative rule. A finding is raised for each element of <see cref="AppliesTo"/> that
    /// matches <see cref="When"/> (or every such element when <see cref="When"/> is omitted) and fails
    /// <see cref="Assert"/> (or unconditionally when <see cref="Assert"/> is omitted). At least one of
    /// <see cref="When"/> or <see cref="Assert"/> must be present.
    /// </summary>
    internal sealed class DeclarativeRuleSpec
    {
        /// <summary>Gets or sets the unique rule id in the pack's own namespace (for example <c>ACME001</c>).</summary>
        public string? Id { get; set; }

        /// <summary>Gets or sets the identifier of the rule pack this rule belongs to.</summary>
        public string? Pack { get; set; }

        /// <summary>Gets or sets the severity (<c>error</c>, <c>warning</c>, or <c>info</c>); defaults to <c>warning</c>.</summary>
        public string? Severity { get; set; }

        /// <summary>Gets or sets the DFD primitive the rule targets (<c>process</c>, <c>datastore</c>, <c>external</c>, or <c>flow</c>).</summary>
        public string? AppliesTo { get; set; }

        /// <summary>Gets or sets the finding message; the token <c>{name}</c> is replaced with the element's display text.</summary>
        public string? Message { get; set; }

        /// <summary>Gets or sets the long description of what the rule checks and why.</summary>
        public string? FullDescription { get; set; }

        /// <summary>Gets or sets guidance on how to resolve a finding.</summary>
        public string? HelpText { get; set; }

        /// <summary>Gets or sets an optional documentation URL.</summary>
        public string? HelpUri { get; set; }

        /// <summary>Gets or sets the optional STRIDE category the finding represents (enables threat generation).</summary>
        public string? Stride { get; set; }

        /// <summary>Gets or sets the optional external references (for example <c>CWE:319</c>, <c>CAPEC:157</c>, <c>ATTACK:T1040</c>).</summary>
        public List<string>? ThreatReferences { get; set; }

        /// <summary>Gets or sets the optional guard; the rule only evaluates elements that match it.</summary>
        public DeclarativeCondition? When { get; set; }

        /// <summary>Gets or sets the requirement; a matching element that fails it produces a finding.</summary>
        public DeclarativeCondition? Assert { get; set; }
    }
}
