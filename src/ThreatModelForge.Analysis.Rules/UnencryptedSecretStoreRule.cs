namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Rule that checks that storage components holding credentials are encrypted at rest.
    /// </summary>
    /// <remarks>
    /// A data store flagged as storing credentials (<c>StoresCredentials = Yes</c>) that is not encrypted
    /// at rest (<c>Encrypted = No</c> or unset) exposes those secrets to information disclosure if the
    /// underlying storage, snapshot, or backup is compromised or copied.
    /// </remarks>
    public class UnencryptedSecretStoreRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnencryptedSecretStoreRule"/> class.
        /// </summary>
        public UnencryptedSecretStoreRule()
            : base(RuleIDs.UnencryptedSecretStoreRule, MessageSeverity.Warning, RulePackCatalog.DataProtection)
        {
            this.FullDescription = UnencryptedSecretStoreRuleResources.FullDescription;
            this.HelpText = UnencryptedSecretStoreRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => new[]
        {
            new PropertyBinding("datastore", "Encrypted", ControlEvidenceValues.Unknown, "No"),
            new PropertyBinding("datastore", "StoresCredentials"),
        };

        /// <inheritdoc/>
        public override StrideCategory? Stride => StrideCategory.InformationDisclosure;

        /// <inheritdoc/>
        public override IReadOnlyList<ThreatReference> ThreatReferences => new[]
        {
            ThreatReference.Cwe(311),
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

                    component.TryGetCustomPropertyValue("Encrypted", out string? encrypted);
                    ControlEvidence encryption = ControlEvidenceValues.ClassifyByAbsentValues(encrypted, "No");
                    if (encryption != ControlEvidence.Present)
                    {
                        string template = encryption == ControlEvidence.Unevidenced
                            ? UnencryptedSecretStoreRuleResources.MessageTextUnevidenced
                            : UnencryptedSecretStoreRuleResources.MessageText;
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
