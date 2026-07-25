namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Rule that checks that a data store holding credentials enforces meaningful access control.
    /// </summary>
    /// <remarks>
    /// A data store flagged as storing credentials (<c>StoresCredentials = Yes</c>) whose access control is
    /// <c>None</c>, <c>Public</c>, or unset lets any party that can reach the store read or tamper with the
    /// secrets it holds, leading to information disclosure and elevation of privilege.
    /// </remarks>
    public class UnprotectedCredentialStoreRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnprotectedCredentialStoreRule"/> class.
        /// </summary>
        public UnprotectedCredentialStoreRule()
            : base(RuleIDs.UnprotectedCredentialStoreRule, MessageSeverity.Warning, RulePackCatalog.DataProtection)
        {
            this.FullDescription = UnprotectedCredentialStoreRuleResources.FullDescription;
            this.HelpText = UnprotectedCredentialStoreRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => new[]
        {
            new PropertyBinding("datastore", "AccessControl", ControlEvidenceValues.Unknown, "None", "Public"),
            new PropertyBinding("datastore", "StoresCredentials"),
        };

        /// <inheritdoc/>
        public override StrideCategory? Stride => StrideCategory.InformationDisclosure;

        /// <inheritdoc/>
        public override IReadOnlyList<ThreatReference> ThreatReferences => new[]
        {
            ThreatReference.Cwe(284),
        };

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));

            foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
            {
                foreach (Entity component in diagram.Components())
                {
                    if (!component.IsStorageComponent())
                    {
                        continue;
                    }

                    if (!component.TryGetCustomPropertyValue("StoresCredentials", out string? storesCredentials) ||
                        !string.Equals(storesCredentials, "Yes", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Only role- or list-based access control restricts who can reach the store, so the
                    // allow list is the evidence. "None" and "Public" are a stated absence; absent,
                    // blank, and "Unknown" mean nobody recorded the control at all.
                    component.TryGetCustomPropertyValue("AccessControl", out string? accessControl);
                    ControlEvidence access = ControlEvidenceValues.ClassifyByPresentValues(accessControl, "RBAC", "ACL");
                    if (access != ControlEvidence.Present)
                    {
                        string template = access == ControlEvidence.Unevidenced
                            ? UnprotectedCredentialStoreRuleResources.MessageTextUnevidenced
                            : UnprotectedCredentialStoreRuleResources.MessageText;
                        string text = string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            template,
                            GetEntityDisplayText(component));
                        context.Writer.Write(this.CreateMessage(component, diagram, text));
                    }
                }
            }
        }
    }
}
