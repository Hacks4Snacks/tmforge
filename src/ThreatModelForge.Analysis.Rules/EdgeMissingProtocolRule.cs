namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using ThreatModelForge.Model;

    /// <summary>
    /// Rule that checks that an edge is missing model information about the protocol being used.
    /// </summary>
    public class EdgeMissingProtocolRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeMissingProtocolRule"/> class.
        /// </summary>
        public EdgeMissingProtocolRule()
            : base(RuleIDs.EdgeMissingProtocolRule, MessageSeverity.Error, RulePackCatalog.StrideCompleteness)
        {
            this.FullDescription = EdgeMissingProtocolRuleResources.FullDescription;
            this.HelpText = EdgeMissingProtocolRuleResources.HelpText;
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

                    if (EdgeProtocolInfo.FromEdge(c, infoSet) == null)
                    {
                        string text = string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            Properties.Resources.EdgeMissingProtocolInfoMessageText,
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
