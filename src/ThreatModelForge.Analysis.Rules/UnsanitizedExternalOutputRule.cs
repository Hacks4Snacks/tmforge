namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Linq;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Rule that checks that a process sending output to an external entity sanitizes that output.
    /// </summary>
    /// <remarks>
    /// A process that sends a data flow to an external interactor is emitting data into a less trusted zone.
    /// If it does not sanitize or encode that output (<c>SanitizesOutput = No</c> or unset), attacker-influenced
    /// data can be reflected to a client (cross-site scripting) or leak sensitive information (information disclosure).
    /// </remarks>
    public class UnsanitizedExternalOutputRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnsanitizedExternalOutputRule"/> class.
        /// </summary>
        public UnsanitizedExternalOutputRule()
            : base(RuleIDs.UnsanitizedExternalOutputRule, MessageSeverity.Warning, RulePackCatalog.InputValidation)
        {
            this.FullDescription = UnsanitizedExternalOutputRuleResources.FullDescription;
            this.HelpText = UnsanitizedExternalOutputRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => new[]
        {
            new PropertyBinding("process", "SanitizesOutput", "No"),
        };

        /// <inheritdoc/>
        public override StrideCategory? Stride => StrideCategory.Tampering;

        /// <inheritdoc/>
        public override IReadOnlyList<ThreatReference> ThreatReferences => new[]
        {
            ThreatReference.Cwe(116),
            ThreatReference.Capec(63),
        };

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));

            foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
            {
                foreach (Entity component in diagram.Components())
                {
                    // Only processes produce output to sanitize: skip data stores and external interactors.
                    if (component.IsStorageComponent() || component.IsExternalInteractor())
                    {
                        continue;
                    }

                    if (SanitizesOutput(component))
                    {
                        continue;
                    }

                    if (HasOutboundToExternalInteractor(diagram, component))
                    {
                        string text = string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            UnsanitizedExternalOutputRuleResources.MessageText,
                            GetEntityDisplayText(component));
                        context.Writer.Write(this.CreateMessage(component, diagram, text));
                    }
                }
            }
        }

        private static bool SanitizesOutput(Entity component)
        {
            return component.TryGetCustomPropertyValue("SanitizesOutput", out string? value) &&
                string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasOutboundToExternalInteractor(DrawingSurfaceModel diagram, Entity component)
        {
            foreach (Connector connector in diagram.Lines.Values.OfType<Connector>())
            {
                if (connector.SourceGuid != component.Guid)
                {
                    continue;
                }

                if (diagram.Borders.TryGetValue(connector.TargetGuid, out object? target) &&
                    target is Entity entity &&
                    entity.IsExternalInteractor())
                {
                    return true;
                }
            }

            return false;
        }
    }
}
