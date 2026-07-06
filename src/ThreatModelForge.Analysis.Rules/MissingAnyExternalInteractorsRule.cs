namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Linq;
    using ThreatModelForge.Model;

    /// <summary>
    /// Rule that checks at least one diagram must have at least on external interactor.
    /// </summary>
    public class MissingAnyExternalInteractorsRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MissingAnyExternalInteractorsRule"/> class.
        /// </summary>
        public MissingAnyExternalInteractorsRule()
            : base(RuleIDs.MissingAnyExternalInteractorsRule, MessageSeverity.Warning, RulePackCatalog.StrideCompleteness)
        {
            this.FullDescription = MissingAnyExternalInteractorsRuleResources.FullDescription;
            this.HelpText = MissingAnyExternalInteractorsRuleResources.HelpText;
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
                if (diagram.ExternalInteractors().Any())
                {
                    return;
                }
            }

            Message message = this.CreateMessage(
                Properties.Resources.NoExternalInteractorDefinedInModel);

            context.Writer.Write(message);
        }
    }
}
