namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Rule that flags a flow carrying sensitive data whose destination is an external interactor, so the
    /// data leaves the system's trust into a party the model does not control.
    /// </summary>
    /// <remarks>
    /// A flow whose data classification (<c>DataType</c>) is sensitive — end-user identifiable or
    /// pseudonymous information, customer content, account data, or access-control data — that terminates
    /// at an external interactor sends that data outside the system boundary. Unless the disclosure is
    /// intended and the external party is authorized to receive it, this is an information-disclosure path
    /// that should be justified or removed.
    /// </remarks>
    public class SensitiveDataToExternalRule : Rule
    {
        /// <summary>
        /// Custom attribute that records the data classification of a flow.
        /// </summary>
        public const string DataTypeCustomAttributeName = "DataType";

        /// <summary>
        /// The data classifications treated as sensitive for the purpose of this rule.
        /// </summary>
        private static readonly HashSet<string> SensitiveClassifications = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "EUII",
            "EUPI",
            "Customer Content",
            "Account Data",
            "Access Control Data",
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="SensitiveDataToExternalRule"/> class.
        /// </summary>
        public SensitiveDataToExternalRule()
            : base(RuleIDs.SensitiveDataToExternalRule, MessageSeverity.Warning, RulePackCatalog.DataProtection)
        {
            this.FullDescription = SensitiveDataToExternalRuleResources.FullDescription;
            this.HelpText = SensitiveDataToExternalRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => new[]
        {
            new PropertyBinding("flow", DataTypeCustomAttributeName, "EUII", "EUPI", "Customer Content", "Account Data", "Access Control Data"),
        };

        /// <inheritdoc/>
        public override StrideCategory? Stride => StrideCategory.InformationDisclosure;

        /// <inheritdoc/>
        public override IReadOnlyList<ThreatReference> ThreatReferences => new[]
        {
            ThreatReference.Cwe(200),
        };

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));

            foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
            {
                foreach (Connector connector in diagram.Lines.Values.OfType<Connector>())
                {
                    if (!TryGetSensitiveClassification(connector, out string classification))
                    {
                        continue;
                    }

                    if (!TargetIsExternalInteractor(diagram, connector))
                    {
                        continue;
                    }

                    string text = string.Format(
                        System.Globalization.CultureInfo.CurrentCulture,
                        SensitiveDataToExternalRuleResources.MessageText,
                        GetEntityDisplayText(connector),
                        classification);
                    context.Writer.Write(this.CreateMessage(connector, diagram, text));
                }
            }
        }

        private static bool TryGetSensitiveClassification(Connector connector, out string classification)
        {
            classification = string.Empty;
            if (!connector.TryGetCustomPropertyValue(DataTypeCustomAttributeName, out string? value) ||
                string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value!.Trim();
            if (!SensitiveClassifications.Contains(trimmed))
            {
                return false;
            }

            classification = trimmed;
            return true;
        }

        private static bool TargetIsExternalInteractor(DrawingSurfaceModel diagram, Connector connector)
        {
            return diagram.Borders.TryGetValue(connector.TargetGuid, out object? value) &&
                value is Entity target &&
                target.IsExternalInteractor();
        }
    }
}
