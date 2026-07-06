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
            new PropertyBinding("datastore", "AccessControl", "None", "Public"),
            new PropertyBinding("datastore", "StoresCredentials"),
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

                    component.TryGetCustomPropertyValue("AccessControl", out string? accessControl);
                    if (!HasMeaningfulAccessControl(accessControl))
                    {
                        string text = string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            UnprotectedCredentialStoreRuleResources.MessageText,
                            GetEntityDisplayText(component));
                        context.Writer.Write(this.CreateMessage(component, diagram, text));
                    }
                }
            }
        }

        private static bool HasMeaningfulAccessControl(string? accessControl)
        {
            // Only role- or list-based access control restricts who can reach the store. An unset value
            // and the "None" or "Public" values are treated as no meaningful access control.
            return string.Equals(accessControl, "RBAC", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(accessControl, "ACL", StringComparison.OrdinalIgnoreCase);
        }
    }
}
