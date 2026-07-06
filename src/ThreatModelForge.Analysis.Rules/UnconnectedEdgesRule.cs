namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Linq;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Rule that identifies edges in the model that are not connected to two components.
    /// </summary>
    public class UnconnectedEdgesRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnconnectedEdgesRule"/> class.
        /// </summary>
        public UnconnectedEdgesRule()
            : base(RuleIDs.UnconnectedEdgesRule, MessageSeverity.Error, RulePackCatalog.CoreHygiene)
        {
            this.FullDescription = UnconnectedEdgesRuleResources.FullDescription;
            this.HelpText = UnconnectedEdgesRuleResources.HelpText;
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
                foreach (Connector c in diagram.Lines.Values.OfType<Connector>())
                {
                    if (c.SourceGuid != Guid.Empty && c.TargetGuid != Guid.Empty)
                    {
                        continue;
                    }

                    // generate an error. the connector is missing a source or target component.
                    Message m = this.GenerateViolation(diagram, c);
                    context.Writer.Write(m);
                }
            }
        }

        private static string GenerateText(
            DrawingSurfaceModel diagram,
            Connector connector)
        {
            string name = GetEntityDisplayText(connector);
            Entity? sourceEntity = null;
            if (diagram.Borders.TryGetValue(connector.SourceGuid, out object source))
            {
                sourceEntity = source as Entity;
            }

            Entity? targetEntity = null;
            if (diagram.Borders.TryGetValue(connector.TargetGuid, out object target))
            {
                targetEntity = target as Entity;
            }

            string sourceName = sourceEntity != null ?
                GetEntityDisplayText(sourceEntity) :
                string.Empty;
            string targetName = targetEntity != null ?
                GetEntityDisplayText(targetEntity) :
                string.Empty;

            string formatString = sourceEntity == null ?
                (targetEntity == null ?
                    Properties.Resources.UnconnectedEdgeMissingSourceAndTargetMessageText :
                    Properties.Resources.UnconnectedEdgeMissingSourceMessageText) :
                Properties.Resources.UnconnectedEdgeMissingTargetMessageText;

            return string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                formatString,
                name,
                sourceName,
                targetName);
        }

        private Message GenerateViolation(DrawingSurfaceModel diagram, Connector connector)
        {
            Message error = this.CreateMessage(
                connector,
                diagram,
                GenerateText(diagram, connector));

            return error;
        }
    }
}
