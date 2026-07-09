namespace ThreatModelForge.Analysis.Rules;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

/// <summary>
/// Configuration for a set of understood protocols that can be recognized by rules.
/// </summary>
public class ProtocolInfoSet
{
    /// <summary>
    /// Variable name for the set of understood protocols.
    /// </summary>
    public const string VariableName = "PROTOCOLS";

    private static readonly IReadOnlyList<ProtocolInfo> WellKnownProtocols = new ProtocolInfo[]
    {
        new ("FTP", 21),
        new ("SSH", 22),
        new ("Telnet", 23),
        new ("SMTP", 25),
        new ("DNS", 53),
        new ("HTTP", 80),
        new ("POP2", 109),
        new ("POP3", 110),
        new ("RPC", 135),
        new ("IMAP", 143),
        new ("SNMP", 161),
        new ("SNMPTRAP", 162),
        new ("IRC", 194),
        new ("LDAP", 389),
        new ("HTTPS", 443),
        new ("SMB", 445),
        new ("SMTPS", 465),
        new ("LDAPS", 636),
        new ("IMAPS", 993),
        new ("POP3S", 995),

        // Modern application protocols recommended by the authoring schema (Stencils/property-schema.json).
        new ("TLS", 443),
        new ("mTLS", 443),
        new ("gRPC", 443),
        new ("AMQP", 5672),
        new ("SQL", 1433),
    };

    private readonly Dictionary<string, ProtocolInfo> protocols = new (StringComparer.OrdinalIgnoreCase);

    private ProtocolInfoSet(IEnumerable<ProtocolInfo> protocols)
    {
        foreach (var p in protocols)
        {
            this.protocols.Add(p.Name, p);
        }
    }

    /// <summary>
    /// Gets the default protocol info set with well known protocols.
    /// </summary>
    public static ProtocolInfoSet Default { get; } = new ProtocolInfoSet(WellKnownProtocols);

    /// <summary>
    /// Gets the configured protocols.
    /// </summary>
    public IReadOnlyDictionary<string, ProtocolInfo> Protocols => new ReadOnlyDictionary<string, ProtocolInfo>(this.protocols);

    /// <summary>
    /// Creates a set from the variable in the context if provided.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>
    /// The parsed variable or <see cref="ProtocolInfoSet.Default"/> if not found.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> is <langword ref="null"/>.
    /// </exception>
    public static ProtocolInfoSet FromContext(RuleEvaluationContext context)
    {
        _ = context ?? throw new ArgumentNullException(nameof(context));
        if (!context.Variables.TryGetValue(VariableName, out string value))
        {
            return Default;
        }

        // Merge the supplied entries onto the well-known defaults so a custom PROTOCOLS augments
        // (and overrides by name) the built-in set rather than replacing it.
        Dictionary<string, ProtocolInfo> merged = new (StringComparer.OrdinalIgnoreCase);
        foreach (var known in WellKnownProtocols)
        {
            merged[known.Name] = known;
        }

        var entries = value
            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim())
            .Where(e => !string.IsNullOrEmpty(e));
        var nameValuePairs = entries
            .Select(entry => entry
                .Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim())
                .Where(e => !string.IsNullOrEmpty(e))
                .ToArray())
            .Where(nameValuePair => nameValuePair.Length == 2);
        foreach (var nameValuePair in nameValuePairs)
        {
            string name = nameValuePair[0];
            if (!int.TryParse(nameValuePair[1], NumberStyles.None, CultureInfo.InvariantCulture, out int port))
            {
                continue;
            }

            merged[name] = new ProtocolInfo(name, port);
        }

        return new (merged.Values);
    }
}
