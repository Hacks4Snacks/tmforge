namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Identifies the source artifact from which a versioned rule pack was produced.
    /// </summary>
    public sealed class RulePackSource
    {
        /// <summary>Initializes a new instance of the <see cref="RulePackSource"/> class.</summary>
        /// <param name="type">The namespaced source type.</param>
        /// <param name="name">The optional source name.</param>
        /// <param name="id">The optional source identifier.</param>
        /// <param name="version">The optional source version.</param>
        /// <param name="uri">The optional source URI.</param>
        /// <param name="fingerprint">The optional source fingerprint.</param>
        /// <param name="metadata">Preserved source metadata.</param>
        internal RulePackSource(
            string type,
            string? name,
            string? id,
            string? version,
            string? uri,
            string? fingerprint,
            IReadOnlyDictionary<string, string> metadata)
        {
            this.Type = type;
            this.Name = name;
            this.Id = id;
            this.Version = version;
            this.Uri = uri;
            this.Fingerprint = fingerprint;
            this.Metadata = metadata ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }

        /// <summary>Gets the namespaced source type.</summary>
        public string Type { get; }

        /// <summary>Gets the source name, when applicable.</summary>
        public string? Name { get; }

        /// <summary>Gets the source identifier, when applicable.</summary>
        public string? Id { get; }

        /// <summary>Gets the source version, when applicable.</summary>
        public string? Version { get; }

        /// <summary>Gets the source URI, when applicable.</summary>
        public string? Uri { get; }

        /// <summary>Gets the source content fingerprint, when applicable.</summary>
        public string? Fingerprint { get; }

        /// <summary>Gets importer-neutral source metadata not otherwise represented.</summary>
        public IReadOnlyDictionary<string, string> Metadata { get; }
    }
}
