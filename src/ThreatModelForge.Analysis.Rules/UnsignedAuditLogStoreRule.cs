namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Rule that checks that a data store holding log or audit data is signed to prevent tampering.
    /// </summary>
    /// <remarks>
    /// A data store flagged as holding log or audit data (<c>StoresLogData = Yes</c>) that is not signed
    /// (<c>Signed = No</c> or unset) lets an attacker alter or delete records without detection, enabling
    /// tampering and repudiation of the events the log is meant to prove.
    /// </remarks>
    public class UnsignedAuditLogStoreRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnsignedAuditLogStoreRule"/> class.
        /// </summary>
        public UnsignedAuditLogStoreRule()
            : base(RuleIDs.UnsignedAuditLogStoreRule, MessageSeverity.Warning, RulePackCatalog.DataProtection)
        {
            this.FullDescription = UnsignedAuditLogStoreRuleResources.FullDescription;
            this.HelpText = UnsignedAuditLogStoreRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => new[]
        {
            new PropertyBinding("datastore", "Signed", "No"),
            new PropertyBinding("datastore", "StoresLogData"),
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

                    if (!component.TryGetCustomPropertyValue("StoresLogData", out string? storesLogData) ||
                        !string.Equals(storesLogData, "Yes", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!IsSigned(component))
                    {
                        string text = string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            UnsignedAuditLogStoreRuleResources.MessageText,
                            GetEntityDisplayText(component));
                        context.Writer.Write(this.CreateMessage(component, diagram, text));
                    }
                }
            }
        }

        private static bool IsSigned(Entity component)
        {
            return component.TryGetCustomPropertyValue("Signed", out string? value) &&
                string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
