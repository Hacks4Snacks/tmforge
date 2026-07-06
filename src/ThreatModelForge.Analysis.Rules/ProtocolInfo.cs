namespace ThreatModelForge.Analysis.Rules;

using System;

/// <summary>
/// Protocol information to associate a named protocol with a default port.
/// </summary>
public class ProtocolInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProtocolInfo"/> class.
    /// </summary>
    /// <param name="name">The protocol name.</param>
    /// <param name="defaultPort">The default port.</param>
    public ProtocolInfo(string name, int defaultPort)
    {
        this.Name = !string.IsNullOrWhiteSpace(name) ? name : throw new ArgumentOutOfRangeException(nameof(name));
        this.DefaultPort = defaultPort;
    }

    /// <summary>
    /// Gets the protocol name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the default port.
    /// </summary>
    public int DefaultPort { get; }
}
