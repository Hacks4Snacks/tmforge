namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A threat projected from a validation finding: the rule that detected it, its STRIDE category and
    /// external references, and the element or flow it was raised against. A generated threat is simply
    /// the persistable, lifecycle-bearing form of a finding — the detection is entirely the rule's.
    /// </summary>
    public sealed class GeneratedThreat
    {
        /// <summary>Gets the deterministic identifier of the threat (<c>{targetGuid:N}:{ruleId}</c>).</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the identifier of the rule that detected the threat (for example <c>TM1023</c>).</summary>
        public string RuleId { get; init; } = string.Empty;

        /// <summary>Gets the STRIDE category.</summary>
        public StrideCategory Category { get; init; }

        /// <summary>Gets the threat title (the finding text).</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>Gets the suggested mitigation (the rule's help text).</summary>
        public string? Mitigation { get; init; }

        /// <summary>Gets the rule severity (<c>error</c>, <c>warning</c>, or <c>info</c>).</summary>
        public string Severity { get; init; } = "warning";

        /// <summary>Gets the coarse priority derived from severity (<c>High</c> / <c>Medium</c> / <c>Low</c>).</summary>
        public string Priority { get; init; } = "Medium";

        /// <summary>Gets the external catalog references.</summary>
        public IReadOnlyList<ThreatReference> References { get; init; } = Array.Empty<ThreatReference>();

        /// <summary>Gets the source element identifier (the target element for an element-scoped finding).</summary>
        public Guid SourceGuid { get; init; }

        /// <summary>Gets the target element identifier (empty for an element-scoped finding).</summary>
        public Guid TargetGuid { get; init; }

        /// <summary>Gets the flow identifier (empty for an element-scoped finding).</summary>
        public Guid FlowGuid { get; init; }

        /// <summary>Gets the diagram identifier.</summary>
        public Guid DiagramGuid { get; init; }

        /// <summary>Gets the source element display name.</summary>
        public string? SourceName { get; init; }

        /// <summary>Gets the target element display name.</summary>
        public string? TargetName { get; init; }

        /// <summary>Gets the flow display name.</summary>
        public string? FlowName { get; init; }

        /// <summary>Gets a value indicating whether the threat is scoped to a flow (an interaction) rather than a single element.</summary>
        public bool IsFlowScoped { get; init; }

        /// <summary>Gets the human-readable scope string (<c>source -&gt; target</c> for a flow, else the element name).</summary>
        public string InteractionString { get; init; } = string.Empty;
    }
}
