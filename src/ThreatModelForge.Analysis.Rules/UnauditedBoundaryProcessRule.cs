namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Linq;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Rule that flags a process receiving input across a trust boundary that has no reachable audit-log
    /// store, so the actions it performs on behalf of callers cannot be attributed after the fact.
    /// </summary>
    /// <remarks>
    /// A process that accepts a data flow crossing a trust boundary is handling requests from a less
    /// trusted zone. If none of its outbound flows reach a data store flagged as holding log or audit data
    /// (<c>StoresLogData = Yes</c>), there is no durable record of who did what, so a caller can later
    /// repudiate an action and an operator cannot reconstruct an incident.
    /// </remarks>
    public class UnauditedBoundaryProcessRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnauditedBoundaryProcessRule"/> class.
        /// </summary>
        public UnauditedBoundaryProcessRule()
            : base(RuleIDs.UnauditedBoundaryProcessRule, MessageSeverity.Warning, RulePackCatalog.StrideCompleteness)
        {
            this.FullDescription = UnauditedBoundaryProcessRuleResources.FullDescription;
            this.HelpText = UnauditedBoundaryProcessRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => new[]
        {
            new PropertyBinding("datastore", "StoresLogData"),
        };

        /// <inheritdoc/>
        public override StrideCategory? Stride => StrideCategory.Repudiation;

        /// <inheritdoc/>
        public override IReadOnlyList<ThreatReference> ThreatReferences => new[]
        {
            ThreatReference.Cwe(778),
        };

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));

            foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
            {
                foreach (Entity component in diagram.Components())
                {
                    // Only processes handle requests: skip data stores and external interactors.
                    if (component.IsStorageComponent() || component.IsExternalInteractor())
                    {
                        continue;
                    }

                    if (!HasInboundTrustBoundaryCrossing(diagram, component))
                    {
                        continue;
                    }

                    if (WritesToAuditLog(diagram, component))
                    {
                        continue;
                    }

                    string text = string.Format(
                        System.Globalization.CultureInfo.CurrentCulture,
                        UnauditedBoundaryProcessRuleResources.MessageText,
                        GetEntityDisplayText(component));
                    context.Writer.Write(this.CreateMessage(component, diagram, text));
                }
            }
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

        private static bool WritesToAuditLog(DrawingSurfaceModel diagram, Entity component)
        {
            return diagram
                .Lines
                .Values
                .OfType<Connector>()
                .Where(c => c.SourceGuid == component.Guid)
                .Any(c => TargetIsAuditLogStore(diagram, c));
        }

        private static bool TargetIsAuditLogStore(DrawingSurfaceModel diagram, Connector connector)
        {
            return diagram.Borders.TryGetValue(connector.TargetGuid, out object? value) &&
                value is Entity target &&
                target.IsStorageComponent() &&
                target.TryGetCustomPropertyValue("StoresLogData", out string? storesLogData) &&
                string.Equals(storesLogData, "Yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
