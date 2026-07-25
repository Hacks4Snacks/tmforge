namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Linq;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Rule that checks that a process receiving input across a trust boundary declares an authentication scheme.
    /// </summary>
    /// <remarks>
    /// A process that accepts a data flow crossing a trust boundary is an entry point into a more trusted
    /// zone. If it declares no authentication scheme (<c>AuthenticationScheme = None</c> or unset), a caller
    /// on the other side of the boundary can be spoofed or can elevate privilege by invoking the process
    /// directly.
    /// </remarks>
    public class UnauthenticatedBoundaryProcessRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnauthenticatedBoundaryProcessRule"/> class.
        /// </summary>
        public UnauthenticatedBoundaryProcessRule()
            : base(RuleIDs.UnauthenticatedBoundaryProcessRule, MessageSeverity.Warning, RulePackCatalog.IdentityAccess)
        {
            this.FullDescription = UnauthenticatedBoundaryProcessRuleResources.FullDescription;
            this.HelpText = UnauthenticatedBoundaryProcessRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => new[]
        {
            new PropertyBinding("process", "AuthenticationScheme", ControlEvidenceValues.Unknown, "None"),
        };

        /// <inheritdoc/>
        public override StrideCategory? Stride => StrideCategory.Spoofing;

        /// <inheritdoc/>
        public override IReadOnlyList<ThreatReference> ThreatReferences => new[]
        {
            ThreatReference.Cwe(306),
            ThreatReference.Capec(115),
        };

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));

            foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
            {
                foreach (Entity component in diagram.Components())
                {
                    // Only processes are entry points to consider: skip data stores and external interactors.
                    if (component.IsStorageComponent() || component.IsExternalInteractor())
                    {
                        continue;
                    }

                    ControlEvidence authentication = ClassifyAuthentication(component);
                    if (authentication == ControlEvidence.Present)
                    {
                        continue;
                    }

                    if (HasInboundTrustBoundaryCrossing(diagram, component))
                    {
                        string template = authentication == ControlEvidence.Unevidenced
                            ? UnauthenticatedBoundaryProcessRuleResources.MessageTextUnevidenced
                            : UnauthenticatedBoundaryProcessRuleResources.MessageText;
                        string text = string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            template,
                            GetEntityDisplayText(component));
                        context.Writer.Write(this.CreateMessage(component, diagram, text));
                    }
                }
            }
        }

        private static ControlEvidence ClassifyAuthentication(Entity component)
        {
            component.TryGetCustomPropertyValue("AuthenticationScheme", out string? scheme);

            // An unevidenced scheme (absent, blank, or "Unknown") must never read as authenticated:
            // treating "we did not record this" as a control in place is exactly how a model comes to
            // look clean while the process is wide open.
            return ControlEvidenceValues.ClassifyByAbsentValues(scheme, "None");
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
