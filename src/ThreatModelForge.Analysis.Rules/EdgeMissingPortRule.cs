namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using ThreatModelForge.Model;

    /// <summary>
    /// Rule that checks for missing port information on an edge if it cannot be inferred from protocol.
    /// </summary>
    public class EdgeMissingPortRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeMissingPortRule"/> class.
        /// </summary>
        public EdgeMissingPortRule()
            : base(RuleIDs.EdgeMissingPortRule, MessageSeverity.Warning, RulePackCatalog.StrideCompleteness)
        {
            this.FullDescription = EdgeMissingPortRuleResources.FullDescription;
            this.HelpText = EdgeMissingPortRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => new[]
        {
            new PropertyBinding("flow", "Protocol"),
            new PropertyBinding("flow", "Port"),
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

                    EdgeProtocolInfo? info = EdgeProtocolInfo.FromEdge(c, infoSet);
                    if (info == null || info.Protocol.DefaultPort <= 0)
                    {
                        string text = string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            Properties.Resources.EdgeMissingPortInfoMessageText,
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
