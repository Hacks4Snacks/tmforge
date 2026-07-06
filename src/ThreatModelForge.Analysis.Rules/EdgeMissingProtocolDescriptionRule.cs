namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using ThreatModelForge.Model;

    /// <summary>
    /// Rule that checks that an edge includes its protocol in the description text.
    /// </summary>
    /// <remarks>
    /// This is different than the <see cref="EdgeMissingProtocolRule"/> because it only
    /// addresses readability of the model and not information in the model. In the future,
    /// this rule can be auto-corrected where <see cref="EdgeMissingProtocolRule"/> requires editing.
    /// </remarks>
    /// <seealso cref="EdgeMissingProtocolRule"/>
    public class EdgeMissingProtocolDescriptionRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeMissingProtocolDescriptionRule"/> class.
        /// </summary>
        public EdgeMissingProtocolDescriptionRule()
            : base(RuleIDs.EdgeMissingProtocolDescriptionRule, MessageSeverity.Info, RulePackCatalog.StrideCompleteness)
        {
            this.FullDescription = EdgeMissingProtocolDescriptionRuleResources.FullDescription;
            this.HelpText = EdgeMissingProtocolDescriptionRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => new[]
        {
            new PropertyBinding("flow", "Protocol"),
        };

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));
            ProtocolInfoSet infoSet = ProtocolInfoSet.FromContext(context);
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

                    if (!EdgeChannelInfo.IsNetworkChannel(c))
                    {
                        continue;
                    }

                    // A protocol declared as a property (for example Protocol=gRPC) satisfies this
                    // readability rule too, so declaring the protocol once clears the protocol
                    // findings; otherwise a protocol named in the label text still satisfies it.
                    bool declaredByProperty =
                        c.TryGetCustomPropertyValue(EdgeProtocolInfo.ProtocolCustomAttributeName, out string? declaredProtocol) &&
                        !string.IsNullOrWhiteSpace(declaredProtocol);

                    if (!declaredByProperty && EdgeProtocolInfo.FromEdgeText(c, infoSet) == null)
                    {
                        string text = string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            Properties.Resources.EdgeMissingProtocolDescriptionMessageText,
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
