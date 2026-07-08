namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Rule that checks that a data store recording log data does not also store credentials.
    /// </summary>
    /// <remarks>
    /// A data store flagged as recording log data (<c>StoresLogData = Yes</c>) that also stores credentials
    /// (<c>StoresCredentials = Yes</c>) is a strong signal that secrets are being written into logs, where
    /// they are typically broadly readable and long-lived, leading to information disclosure.
    /// </remarks>
    public class CredentialsInLogStoreRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CredentialsInLogStoreRule"/> class.
        /// </summary>
        public CredentialsInLogStoreRule()
            : base(RuleIDs.CredentialsInLogStoreRule, MessageSeverity.Warning, RulePackCatalog.DataProtection)
        {
            this.FullDescription = CredentialsInLogStoreRuleResources.FullDescription;
            this.HelpText = CredentialsInLogStoreRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => new[]
        {
            new PropertyBinding("datastore", "StoresLogData"),
            new PropertyBinding("datastore", "StoresCredentials"),
        };

        /// <inheritdoc/>
        public override StrideCategory? Stride => StrideCategory.InformationDisclosure;

        /// <inheritdoc/>
        public override IReadOnlyList<ThreatReference> ThreatReferences => new[]
        {
            ThreatReference.Cwe(532),
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

                    if (IsYes(component, "StoresLogData") && IsYes(component, "StoresCredentials"))
                    {
                        string text = string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            CredentialsInLogStoreRuleResources.MessageText,
                            GetEntityDisplayText(component));
                        context.Writer.Write(this.CreateMessage(component, diagram, text));
                    }
                }
            }
        }

        private static bool IsYes(Entity component, string propertyName)
        {
            return component.TryGetCustomPropertyValue(propertyName, out string? value) &&
                string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
