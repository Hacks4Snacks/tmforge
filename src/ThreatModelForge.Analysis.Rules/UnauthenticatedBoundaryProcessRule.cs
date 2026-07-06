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
            new PropertyBinding("process", "AuthenticationScheme", "None"),
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

                    if (IsAuthenticated(component))
                    {
                        continue;
                    }

                    if (HasInboundTrustBoundaryCrossing(diagram, component))
                    {
                        string text = string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            UnauthenticatedBoundaryProcessRuleResources.MessageText,
                            GetEntityDisplayText(component));
                        context.Writer.Write(this.CreateMessage(component, diagram, text));
                    }
                }
            }
        }

        private static bool IsAuthenticated(Entity component)
        {
            if (!component.TryGetCustomPropertyValue("AuthenticationScheme", out string? scheme))
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(scheme) &&
                !string.Equals(scheme, "None", StringComparison.OrdinalIgnoreCase);
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
