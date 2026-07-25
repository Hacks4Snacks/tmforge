namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Linq;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Rule that checks that a process receiving input across a trust boundary sanitizes that input.
    /// </summary>
    /// <remarks>
    /// A process that accepts a data flow crossing a trust boundary is receiving input from a less trusted
    /// zone. If it does not sanitize that input (<c>SanitizesInput = No</c> or unset), the input can carry
    /// injection or tampering payloads (SQL/command/script injection) into the more trusted zone.
    /// </remarks>
    public class UnsanitizedCrossBoundaryInputRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnsanitizedCrossBoundaryInputRule"/> class.
        /// </summary>
        public UnsanitizedCrossBoundaryInputRule()
            : base(RuleIDs.UnsanitizedCrossBoundaryInputRule, MessageSeverity.Warning, RulePackCatalog.InputValidation)
        {
            this.FullDescription = UnsanitizedCrossBoundaryInputRuleResources.FullDescription;
            this.HelpText = UnsanitizedCrossBoundaryInputRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => new[]
        {
            new PropertyBinding("process", "SanitizesInput", ControlEvidenceValues.Unknown, "No"),
        };

        /// <inheritdoc/>
        public override StrideCategory? Stride => StrideCategory.Tampering;

        /// <inheritdoc/>
        public override IReadOnlyList<ThreatReference> ThreatReferences => new[]
        {
            ThreatReference.Cwe(20),
            ThreatReference.Capec(66),
        };

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));

            foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
            {
                foreach (Entity component in diagram.Components())
                {
                    // Only processes handle input: skip data stores and external interactors.
                    if (component.IsStorageComponent() || component.IsExternalInteractor())
                    {
                        continue;
                    }

                    ControlEvidence sanitization = ClassifyInputSanitization(component);
                    if (sanitization == ControlEvidence.Present)
                    {
                        continue;
                    }

                    if (HasInboundTrustBoundaryCrossing(diagram, component))
                    {
                        string template = sanitization == ControlEvidence.Unevidenced
                            ? UnsanitizedCrossBoundaryInputRuleResources.MessageTextUnevidenced
                            : UnsanitizedCrossBoundaryInputRuleResources.MessageText;
                        string text = string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            template,
                            GetEntityDisplayText(component));
                        context.Writer.Write(this.CreateMessage(component, diagram, text));
                    }
                }
            }
        }

        private static ControlEvidence ClassifyInputSanitization(Entity component)
        {
            component.TryGetCustomPropertyValue("SanitizesInput", out string? value);
            return ControlEvidenceValues.ClassifyByPresentValues(value, "Yes");
        }

        private static bool HasInboundTrustBoundaryCrossing(DrawingSurfaceModel diagram, Entity component)
        {
            return diagram
                .Lines
                .Values
                .OfType<Connector>()
                .Where(c => c.TargetGuid == component.Guid)
                .Any(c => diagram.TrustBoundaryCrossings(c).Any());
        }
    }
}
