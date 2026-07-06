namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ThreatModelForge.Model;

    /// <summary>
    /// Rule that checks that an edge crossing a trust boundary does not use a well-known cleartext protocol.
    /// </summary>
    /// <remarks>
    /// A data flow that crosses a trust boundary leaves a zone of control, so it is exposed to tampering and
    /// information disclosure on the wire. When such an edge declares a cleartext protocol (for example HTTP,
    /// FTP, or Telnet), the data crossing the boundary is unprotected in transit.
    /// </remarks>
    public class CleartextTrustBoundaryCrossingRule : Rule
    {
        /// <summary>
        /// Well-known protocols that transmit data in cleartext (that is, without transport encryption).
        /// </summary>
        private static readonly HashSet<string> CleartextProtocols = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "HTTP",
            "HTTP/1.0",
            "HTTP/1.1",
            "FTP",
            "TFTP",
            "Telnet",
            "SMTP",
            "IMAP",
            "POP3",
            "LDAP",
            "SNMP",
            "WS",
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="CleartextTrustBoundaryCrossingRule"/> class.
        /// </summary>
        public CleartextTrustBoundaryCrossingRule()
            : base(RuleIDs.CleartextTrustBoundaryCrossingRule, MessageSeverity.Warning, RulePackCatalog.TransportSecurity)
        {
            this.FullDescription = CleartextTrustBoundaryCrossingRuleResources.FullDescription;
            this.HelpText = CleartextTrustBoundaryCrossingRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => new[]
        {
            new PropertyBinding("flow", "Protocol", "HTTP", "FTP"),
        };

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));

            foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
            {
                foreach (Connector connector in diagram.Lines.Values.OfType<Connector>())
                {
                    if (!diagram.TrustBoundaryCrossings(connector).Any())
                    {
                        continue;
                    }

                    if (!EdgeChannelInfo.IsNetworkChannel(connector))
                    {
                        continue;
                    }

                    if (!connector.TryGetCustomPropertyValue("Protocol", out string? protocol) ||
                        string.IsNullOrWhiteSpace(protocol))
                    {
                        continue;
                    }

                    string normalized = protocol!.Trim();
                    if (CleartextProtocols.Contains(normalized))
                    {
                        string text = string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            CleartextTrustBoundaryCrossingRuleResources.MessageText,
                            GetEntityDisplayText(connector),
                            normalized);
                        context.Writer.Write(this.CreateMessage(connector, diagram, text));
                    }
                }
            }
        }
    }
}
