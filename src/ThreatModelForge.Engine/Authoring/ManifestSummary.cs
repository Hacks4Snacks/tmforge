namespace ThreatModelForge.Engine
{
    /// <summary>A count of what a manifest produced or captured.</summary>
    public readonly struct ManifestSummary
    {
        /// <summary>Initializes a new instance of the <see cref="ManifestSummary"/> struct.</summary>
        /// <param name="boundaries">The number of boundaries.</param>
        /// <param name="elements">The number of elements.</param>
        /// <param name="flows">The number of flows.</param>
        public ManifestSummary(int boundaries, int elements, int flows)
        {
            this.Boundaries = boundaries;
            this.Elements = elements;
            this.Flows = flows;
        }

        /// <summary>Gets the number of boundaries.</summary>
        public int Boundaries { get; }

        /// <summary>Gets the number of elements.</summary>
        public int Elements { get; }

        /// <summary>Gets the number of flows.</summary>
        public int Flows { get; }
    }
}
