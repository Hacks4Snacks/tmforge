namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Collections.Generic;
    using ThreatModelForge.Model;

    /// <summary>
    /// Reads the transport <c>Channel</c> declared on an edge. A non-network channel (in-process,
    /// local file, Unix socket, or loopback) has no off-host wire, so the protocol, port, and
    /// cleartext-crossing rules do not apply to it. Edges with no declared channel are treated as
    /// network edges.
    /// </summary>
    public static class EdgeChannelInfo
    {
        /// <summary>
        /// Custom attribute that names an edge's transport channel.
        /// </summary>
        public const string ChannelCustomAttributeName = "Channel";

        /// <summary>Channel values that indicate the edge does not traverse a network.</summary>
        private static readonly HashSet<string> NonNetworkChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "In-Process",
            "InProcess",
            "In Process",
            "Local-file",
            "LocalFile",
            "Local file",
            "Unix-socket",
            "UnixSocket",
            "Unix socket",
            "Unix",
            "Loopback",
            "IPC",
            "Local",
            "None",
        };

        /// <summary>
        /// Returns whether an edge represents a network channel. Edges whose <c>Channel</c> property
        /// names an in-process, local-file, or Unix-socket channel are not network edges, so the
        /// protocol, port, and cleartext-crossing checks are not applicable to them.
        /// </summary>
        /// <param name="edge">The edge to inspect.</param>
        /// <returns>
        /// <see langword="true"/> for a network edge, including when no channel is declared;
        /// <see langword="false"/> for a declared non-network channel.
        /// </returns>
        public static bool IsNetworkChannel(Connector edge)
        {
            _ = edge ?? throw new ArgumentNullException(nameof(edge));
            if (edge.TryGetCustomPropertyValue(ChannelCustomAttributeName, out string? channel) &&
                !string.IsNullOrWhiteSpace(channel))
            {
                return !NonNetworkChannels.Contains(channel!.Trim());
            }

            return true;
        }
    }
}
