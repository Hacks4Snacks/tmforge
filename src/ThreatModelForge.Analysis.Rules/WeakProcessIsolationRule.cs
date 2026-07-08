namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Linq;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Rule that checks that a process receiving input across a trust boundary runs with some isolation.
    /// </summary>
    /// <remarks>
    /// A process that accepts a data flow crossing a trust boundary is exposed to a less trusted zone. If it
    /// runs with no isolation (<c>Isolation = None</c> or unset), a compromise of that process is not
    /// contained: it can reach the host, adjacent services, and shared resources, widening the blast radius.
    /// </remarks>
    public class WeakProcessIsolationRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WeakProcessIsolationRule"/> class.
        /// </summary>
        public WeakProcessIsolationRule()
            : base(RuleIDs.WeakProcessIsolationRule, MessageSeverity.Warning, RulePackCatalog.InputValidation)
        {
            this.FullDescription = WeakProcessIsolationRuleResources.FullDescription;
            this.HelpText = WeakProcessIsolationRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => new[]
        {
            new PropertyBinding("process", "Isolation", "None"),
        };

        /// <inheritdoc/>
        public override StrideCategory? Stride => StrideCategory.ElevationOfPrivilege;

        /// <inheritdoc/>
        public override IReadOnlyList<ThreatReference> ThreatReferences => new[]
        {
            ThreatReference.Cwe(693),
        };

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));

            foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
            {
                foreach (Entity component in diagram.Components())
                {
                    // Only processes execute code and can be isolated: skip data stores and external interactors.
                    if (component.IsStorageComponent() || component.IsExternalInteractor())
                    {
                        continue;
                    }

                    if (IsIsolated(component))
                    {
                        continue;
                    }

                    if (HasInboundTrustBoundaryCrossing(diagram, component))
                    {
                        string text = string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            WeakProcessIsolationRuleResources.MessageText,
                            GetEntityDisplayText(component));
                        context.Writer.Write(this.CreateMessage(component, diagram, text));
                    }
                }
            }
        }

        private static bool IsIsolated(Entity component)
        {
            return component.TryGetCustomPropertyValue("Isolation", out string? value) &&
                !string.IsNullOrWhiteSpace(value) &&
                !string.Equals(value, "None", StringComparison.OrdinalIgnoreCase);
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
