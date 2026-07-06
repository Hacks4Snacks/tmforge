namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Linq;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Rule that checks that outbound edges from storage components correctly describe data flow.
    /// </summary>
    /// <remarks>
    /// An anti-pattern discovered shows data flowing out of a storage component without a corresponding
    /// request from a component into the storage component. Activation starting from a storage component
    /// should indicate a notification where the storage component actively calls into the other component.
    /// This confuses any analysis rules and also human reviewers.
    /// </remarks>
    public class OutboundStorageEdgeRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OutboundStorageEdgeRule"/> class.
        /// </summary>
        public OutboundStorageEdgeRule()
            : base(RuleIDs.OutboundStorageEdgeRule, MessageSeverity.Warning, RulePackCatalog.StrideCompleteness)
        {
            this.FullDescription = OutboundStorageEdgeRuleResources.FullDescription;
            this.HelpText = OutboundStorageEdgeRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
            {
                foreach (Entity component in diagram.Components())
                {
                    if (!component.IsStorageComponent())
                    {
                        continue;
                    }

                    int inbound = 0;
                    int outbound = 0;
                    foreach (Connector c in diagram.Lines.Values.OfType<Connector>())
                    {
                        if (c.SourceGuid == component.Guid)
                        {
                            outbound++;
                        }

                        if (c.TargetGuid == component.Guid)
                        {
                            inbound++;
                        }
                    }

                    if (outbound > inbound)
                    {
                        Message m = this.GenerateViolation(diagram, component, inbound, outbound);
                        context.Writer.Write(m);
                    }
                }
            }
        }

        private static string GenerateText(
            Entity component,
            int inboundEdgeCount,
            int outboundEdgeCount)
        {
            return string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                Properties.Resources.StorageComponentMissingInboundEdgesMessageText,
                GetEntityDisplayText(component),
                inboundEdgeCount,
                outboundEdgeCount);
        }

        private Message GenerateViolation(
            DrawingSurfaceModel diagram,
            Entity component,
            int inboundEdgeCount,
            int outboundEdgeCount)
        {
            return this.CreateMessage(
                component,
                diagram,
                GenerateText(component, inboundEdgeCount, outboundEdgeCount));
        }
    }
}
