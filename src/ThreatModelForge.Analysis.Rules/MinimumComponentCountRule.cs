namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Linq;
    using ThreatModelForge.Model;

    /// <summary>
    /// Rule that checks each diagram has at least 3 components.
    /// </summary>
    public class MinimumComponentCountRule : Rule
    {
        /// <summary>
        /// The minimum number of components that should be in each diagram.
        /// </summary>
        public const int MinimumComponentCount = 3;

        /// <summary>
        /// Initializes a new instance of the <see cref="MinimumComponentCountRule"/> class.
        /// </summary>
        public MinimumComponentCountRule()
            : base(RuleIDs.MinimumComponentCountRule, MessageSeverity.Warning, RulePackCatalog.CoreHygiene)
        {
            this.FullDescription = MinimumComponentCountRuleResources.FullDescription;
            this.HelpText = MinimumComponentCountRuleResources.HelpText;
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
                int componentCount = diagram
                    .Components()
                    .Count();
                if (componentCount < MinimumComponentCount)
                {
                    Message message = this.CreateMessage(
                        diagram,
                        Properties.Resources.MinimumComponentCountString);
                    context.Writer.Write(message);
                }
            }
        }
    }
}
