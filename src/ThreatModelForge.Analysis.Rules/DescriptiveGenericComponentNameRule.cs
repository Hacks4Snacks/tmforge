namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Rule that checks each use of a generic component has a descriptive name.
    /// </summary>
    public class DescriptiveGenericComponentNameRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DescriptiveGenericComponentNameRule"/> class.
        /// </summary>
        public DescriptiveGenericComponentNameRule()
            : base(RuleIDs.DescriptiveGenericComponentNameRule, MessageSeverity.Error, RulePackCatalog.CoreHygiene)
        {
            this.FullDescription = DescriptiveGenericComponentNameRuleResources.FullDescription;
            this.HelpText = DescriptiveGenericComponentNameRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));
            foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
            {
                foreach (Entity c in diagram.Components().Where(c => c.IsGenericComponent()))
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
                            Properties.Resources.GenericComponentNameShouldNotMatchTypeNameMessageText,
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
