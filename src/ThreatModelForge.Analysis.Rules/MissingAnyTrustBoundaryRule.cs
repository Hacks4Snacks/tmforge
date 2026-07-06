namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Linq;
    using ThreatModelForge.Model;

    /// <summary>
    /// Rule that checks that at least one diagram has at least one trust boundary.
    /// </summary>
    public class MissingAnyTrustBoundaryRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MissingAnyTrustBoundaryRule"/> class.
        /// </summary>
        public MissingAnyTrustBoundaryRule()
            : base(RuleIDs.MissingAnyTrustBoundaryRule, MessageSeverity.Error, RulePackCatalog.StrideCompleteness)
        {
            this.FullDescription = MissingAnyTrustBoundaryRuleResources.FullDescription;
            this.HelpText = MissingAnyTrustBoundaryRuleResources.HelpText;
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
                if (diagram.TrustBoundaryBorders().Any() || diagram.TrustBoundaryLines().Any())
                {
                    return;
                }
            }

            Message message = this.CreateMessage(
                Properties.Resources.NoTrustBoundariesDefinedInModel);

            context.Writer.Write(message);
        }
    }
}
