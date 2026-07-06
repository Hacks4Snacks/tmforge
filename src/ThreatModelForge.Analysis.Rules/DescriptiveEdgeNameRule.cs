namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Rule that flags edges without descriptive names, i.e. the name matches the protocol.
    /// </summary>
    public class DescriptiveEdgeNameRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DescriptiveEdgeNameRule"/> class.
        /// </summary>
        public DescriptiveEdgeNameRule()
            : base(RuleIDs.DescriptiveEdgeNameRule, MessageSeverity.Warning, RulePackCatalog.CoreHygiene)
        {
            this.FullDescription = DescriptiveEdgeNameRuleResources.FullDescription;
            this.HelpText = DescriptiveEdgeNameRuleResources.HelpText;
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
                    string? name = c.Name();
                    string? headerName = c.HeaderName();
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(headerName))
                    {
                        continue;
                    }

                    if (string.Equals(headerName!.Trim(), name!.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        string text = string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            Properties.Resources.EdgeNamesShouldNotMatchEdgeTypeMessageText,
                            GetEntityDisplayText(c));
                        Message m = this.CreateMessage(
                            c,
                            diagram,
                            text);
                        context.Writer.Write(m);
                    }
                }
            }
        }
    }
}
