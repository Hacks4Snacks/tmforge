namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Rule that checks that a process does not run as a highly privileged account.
    /// </summary>
    /// <remarks>
    /// A process flagged as running as <c>System</c> or <c>Root/Admin</c> violates least privilege: if it is
    /// compromised, the attacker inherits broad control over the host and adjacent resources, turning a single
    /// flaw into an elevation of privilege with a large blast radius.
    /// </remarks>
    public class OverPrivilegedProcessRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OverPrivilegedProcessRule"/> class.
        /// </summary>
        public OverPrivilegedProcessRule()
            : base(RuleIDs.OverPrivilegedProcessRule, MessageSeverity.Warning, RulePackCatalog.IdentityAccess)
        {
            this.FullDescription = OverPrivilegedProcessRuleResources.FullDescription;
            this.HelpText = OverPrivilegedProcessRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => new[]
        {
            new PropertyBinding("process", "RunningAs", "System", "Root/Admin"),
        };

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));

            foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
            {
                foreach (Entity component in diagram.Components())
                {
                    // Only processes execute with an identity: skip data stores and external interactors.
                    if (component.IsStorageComponent() || component.IsExternalInteractor())
                    {
                        continue;
                    }

                    if (!component.TryGetCustomPropertyValue("RunningAs", out string? runningAs) ||
                        !IsHighlyPrivileged(runningAs))
                    {
                        continue;
                    }

                    string text = string.Format(
                        System.Globalization.CultureInfo.CurrentCulture,
                        OverPrivilegedProcessRuleResources.MessageText,
                        GetEntityDisplayText(component),
                        runningAs);
                    context.Writer.Write(this.CreateMessage(component, diagram, text));
                }
            }
        }

        private static bool IsHighlyPrivileged(string? runningAs)
        {
            return string.Equals(runningAs, "System", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(runningAs, "Root/Admin", StringComparison.OrdinalIgnoreCase);
        }
    }
}
