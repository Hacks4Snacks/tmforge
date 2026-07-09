namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Rule that flags a configurable set of components that do not have a descriptive name.
    /// </summary>
    public class DescriptiveSpecificComponentNameRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DescriptiveSpecificComponentNameRule"/> class.
        /// </summary>
        public DescriptiveSpecificComponentNameRule()
            : base(RuleIDs.DescriptiveSpecificComponentNameRule, MessageSeverity.Warning, RulePackCatalog.CoreHygiene)
        {
            this.FullDescription = DescriptiveSpecificComponentNameRuleResources.FullDescription;
            this.HelpText = DescriptiveSpecificComponentNameRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            GeneralPurposeComponentSet compSet = GeneralPurposeComponentSet.FromContext(context);
            foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
            {
                foreach (Entity c in diagram.Components().Where(c => compSet.IsGeneralPurposeComponent(c)))
                {
                    string? name = c.Name();
                    string? headerName = c.HeaderName();
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(headerName))
                    {
                        continue;
                    }

                    if (string.Equals(name!.Trim(), headerName!.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        string text = string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            Properties.Resources.GeneralPurposeComponentNameShouldNotMatchTypeNameMessageText,
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
