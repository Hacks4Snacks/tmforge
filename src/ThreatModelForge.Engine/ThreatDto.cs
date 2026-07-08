namespace ThreatModelForge.Engine
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A single generated STRIDE threat returned to the client, projected from a validation finding.
    /// </summary>
    public sealed class ThreatDto
    {
        /// <summary>Gets the deterministic threat identifier (register key).</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the identifier of the rule that detected the threat (for example <c>TM1023</c>).</summary>
        public string RuleId { get; init; } = string.Empty;

        /// <summary>Gets the STRIDE category.</summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>Gets the threat title (the finding text).</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>Gets the suggested mitigation (the rule's help text).</summary>
        public string? Mitigation { get; init; }

        /// <summary>Gets the rule severity (<c>error</c>, <c>warning</c>, or <c>info</c>).</summary>
        public string Severity { get; init; } = "warning";

        /// <summary>Gets the coarse priority hint (<c>High</c> / <c>Medium</c> / <c>Low</c>).</summary>
        public string? Priority { get; init; }

        /// <summary>Gets the external catalog reference identifiers (CAPEC / CWE / ATT&amp;CK).</summary>
        public IReadOnlyList<string> References { get; init; } = Array.Empty<string>();

        /// <summary>Gets the element identifiers (source, target, flow) this threat refers to, for highlighting.</summary>
        public IReadOnlyList<string> ElementIds { get; init; } = Array.Empty<string>();

        /// <summary>Gets the human-readable scope string (<c>source -&gt; target</c> for a flow, else the element name).</summary>
        public string Interaction { get; init; } = string.Empty;
    }
}
