namespace ThreatModelForge.Analysis.Rules
{
    using System.Globalization;
    using ThreatModelForge.Model;

    /// <summary>
    /// Describes the method by which the protocol info was derived.
    /// </summary>
    public enum EdgeProtocolSpecificationMethod
    {
        /// <summary>
        /// By default, there is no specification.
        /// </summary>
        None,

        /// <summary>
        /// The protocol is inferred from the stencil type.
        /// </summary>
        ByStencilType,

        /// <summary>
        /// The protocol is explicitly specified in a custom attribute/property.
        /// </summary>
        ByCustomAttribute,

        /// <summary>
        /// The protocol appears as text in the label.
        /// </summary>
        ByText,
    }

    /// <summary>
    /// Information about protocol that is tagged or inferred from an edge.
    /// </summary>
    public class EdgeProtocolInfo
    {
        /// <summary>
        /// Custom attribute that specifies protocol.
        /// </summary>
        public const string ProtocolCustomAttributeName = "Protocol";

        /// <summary>
        /// Custom attribute that specifies port.
        /// </summary>
        public const string PortCustomAttributeName = "Port";

        /// <summary>
        /// Initializes a new instance of the <see cref="EdgeProtocolInfo"/> class.
        /// </summary>
        /// <param name="source">The source connector.</param>
        /// <param name="protocol">The protocol info.</param>
        /// <param name="how">The method used to derive the protocol info.</param>
        public EdgeProtocolInfo(
            Connector source,
            ProtocolInfo protocol,
            EdgeProtocolSpecificationMethod how)
        {
            this.Source = source;
            this.Protocol = protocol;
            this.How = how;
        }

        /// <summary>
        /// Gets the protocol.
        /// </summary>
        public ProtocolInfo Protocol { get; }

        /// <summary>
        /// Gets the source.
        /// </summary>
        public Connector Source { get; }

        /// <summary>
        /// Gets how the protocol was derived from the edge.
        /// </summary>
        public EdgeProtocolSpecificationMethod How { get; }

        /// <summary>
        /// Look for protocol specification using a variety of methods.
        /// </summary>
        /// <param name="edge">The edge to examine.</param>
        /// <param name="infoSet">The set of known protocols.</param>
        /// <returns>
        /// A new instance of the <see cref="EdgeProtocolInfo"/> class or <see langword="null"/> if not info is available.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Either parameter is <see langword="null"/>.
        /// </exception>
        public static EdgeProtocolInfo? FromEdge(Connector edge, ProtocolInfoSet infoSet)
        {
            _ = edge ?? throw new ArgumentNullException(nameof(edge));
            _ = infoSet ?? throw new ArgumentNullException(nameof(infoSet));

            string? headerName = edge.HeaderName();
            if (!string.IsNullOrWhiteSpace(headerName)
                && infoSet.Protocols.TryGetValue(headerName!, out var stencilProto))
            {
                return new EdgeProtocolInfo(edge, stencilProto, EdgeProtocolSpecificationMethod.ByStencilType);
            }

            if (TryGetProtocol(edge, out string? protoAttrib))
            {
                if (TryGetPort(edge, out int? portAttrib))
                {
                    return new EdgeProtocolInfo(
                        edge,
                        new ProtocolInfo(protoAttrib!, portAttrib!.Value),
                        EdgeProtocolSpecificationMethod.ByCustomAttribute);
                }

                if (infoSet.Protocols.TryGetValue(protoAttrib!, out var proto))
                {
                    return new EdgeProtocolInfo(edge, proto, EdgeProtocolSpecificationMethod.ByCustomAttribute);
                }

                return new EdgeProtocolInfo(edge, new ProtocolInfo(protoAttrib!, 0), EdgeProtocolSpecificationMethod.ByCustomAttribute);
            }

            return FromEdgeText(edge, infoSet);
        }

        /// <summary>
        /// Look for protocol specification in the free form text of the name.
        /// </summary>
        /// <param name="edge">The edge to examine.</param>
        /// <param name="infoSet">The set of known protocols.</param>
        /// <returns>
        /// A new instance of the <see cref="EdgeProtocolInfo"/> class or <see langword="null"/> if not info is available.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Either parameter is <see langword="null"/>.
        /// </exception>
        public static EdgeProtocolInfo? FromEdgeText(Connector edge, ProtocolInfoSet infoSet)
        {
            _ = edge ?? throw new ArgumentNullException(nameof(edge));
            _ = infoSet ?? throw new ArgumentNullException(nameof(infoSet));

            IReadOnlyList<string> tokens = edge.Name()?.TokenizeText()?.ToList() ?? new List<string>();
            ProtocolInfo? info = null;

            // look for the protocol to be explcitly called out as a property assignment.
            if (TryGetExplicitProtocolFromText(tokens, out string? explicitProto))
            {
                // look up a known protocol to get its default port.
                if (!infoSet.Protocols.TryGetValue(explicitProto!, out info))
                {
                    info = new ProtocolInfo(explicitProto!, 0);
                }
            }
            else
            {
                // see if a known protocol is mentioned free form.
                string? mentioned = infoSet.Protocols.Keys
                    .FirstOrDefault(protoName => tokens.Contains(protoName, StringComparer.OrdinalIgnoreCase));
                if (mentioned != null)
                {
                    info = infoSet.Protocols[mentioned];
                }
            }

            if (info == null)
            {
                return null;
            }

            // look for a port override.
            if (TryGetExplicitPortFromText(tokens, out int? port))
            {
                info = new ProtocolInfo(info.Name, port!.Value);
            }

            return new EdgeProtocolInfo(edge, info, EdgeProtocolSpecificationMethod.ByText);
        }

        private static bool TryGetExplicitProtocolFromText(
            IReadOnlyList<string> text,
            out string? value) =>
            TryGetExplicitPropertyValueFromText(text, ProtocolCustomAttributeName, out value);

        private static bool TryGetExplicitPortFromText(
            IReadOnlyList<string> text,
            out int? value)
        {
            value = null;
            if (TryGetExplicitPropertyValueFromText(text, PortCustomAttributeName, out string? valueString)
                && int.TryParse(valueString!, NumberStyles.None, CultureInfo.InvariantCulture, out int val))
            {
                value = val;
                return true;
            }

            return false;
        }

        private static bool TryGetExplicitPropertyValueFromText(
            IReadOnlyList<string> text,
            string propertyName,
            out string? value)
        {
            value = null;
            for (int i = 0; i < text.Count - 2; i++)
            {
                if (string.Equals(text[i], propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    string op = text[i + 1];
                    string val = text[i + 2];
                    if (op.Length == 1)
                    {
                        char opChar = op[0];
                        if (opChar == ':' || opChar == '=')
                        {
                            value = val;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool TryGetProtocol(Connector edge, out string? protocol) =>
            edge.TryGetCustomPropertyValue(ProtocolCustomAttributeName, out protocol);

        private static bool TryGetPort(Connector edge, out int? port)
        {
            if (edge.TryGetCustomPropertyValue(PortCustomAttributeName, out string? portString)
                && int.TryParse(portString, NumberStyles.None, CultureInfo.InvariantCulture, out int portVal))
            {
                port = portVal;
                return true;
            }

            port = null;
            return false;
        }
    }
}
