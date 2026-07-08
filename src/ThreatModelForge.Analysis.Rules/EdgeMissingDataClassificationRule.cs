namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using ThreatModelForge.Model;

    /// <summary>
    /// Rule that checks if an edge is missing a data classification.
    /// </summary>
    public class EdgeMissingDataClassificationRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeMissingDataClassificationRule"/> class.
        /// </summary>
        public EdgeMissingDataClassificationRule()
            : base(RuleIDs.EdgeMissingDataClassificationRule, MessageSeverity.Warning, RulePackCatalog.StrideCompleteness)
        {
            this.FullDescription = EdgeMissingDataClassificationRuleResources.FullDescription;
            this.HelpText = EdgeMissingDataClassificationRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => new[]
        {
            new PropertyBinding("flow", "DataType"),
        };

        /// <inheritdoc/>
        public override StrideCategory? Stride => StrideCategory.InformationDisclosure;

        /// <inheritdoc/>
        public override IReadOnlyList<ThreatReference> ThreatReferences => new[]
        {
            ThreatReference.Cwe(200),
        };

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));
            DataClassificationTagSet tagSet = DataClassificationTagSet.FromContext(context);
            foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
            {
                // Look for a default classification. If it is specified, each edge does not need to be examined.
                if (tagSet.TryGetDefaultTag(diagram, out string? _))
                {
                    continue;
                }

                // Otherwise, check each edge for a classification.
                foreach (Connector c in diagram.Lines.Values.OfType<Connector>())
                {
                    if (!tagSet.TryGetTag(c, out string? _))
                    {
                        string text = string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            Properties.Resources.EdgeMissingDataClassificationMessageText,
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
