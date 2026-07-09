namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Linq;
    using ThreatModelForge.Model;

    /// <summary>
    /// A rule that checks at least one diagram must have at least one edge that crosses
    /// at least one trust boundary.
    /// </summary>
    public class MissingAnyTrustBoundaryCrossingRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MissingAnyTrustBoundaryCrossingRule"/> class.
        /// </summary>
        public MissingAnyTrustBoundaryCrossingRule()
            : base(RuleIDs.MissingAnyTrustBoundaryCrossingRule, MessageSeverity.Error, RulePackCatalog.StrideCompleteness)
        {
            this.FullDescription = MissingAnyTrustBoundaryCrossingRuleResources.FullDescription;
            this.HelpText = MissingAnyTrustBoundaryCrossingRuleResources.HelpText;
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
                foreach (Connector c in diagram.Lines.Values.OfType<Connector>().Where(c => diagram.TrustBoundaryCrossings(c).Any()))
                {
                    return;
                }
            }

            Message message = this.CreateMessage(
                Properties.Resources.NoEdgeCrossesAnyTrustBoundaryInModel);

            context.Writer.Write(message);
        }
    }
}
