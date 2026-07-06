namespace ThreatModelForge.Formats
{
    using System;

    /// <summary>
    /// Describes what a <see cref="IThreatModelFormat"/> provider can do and how faithfully it
    /// maps to and from the canonical threat model.
    /// </summary>
    public sealed class FormatCapabilities
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FormatCapabilities"/> class.
        /// </summary>
        /// <param name="canRead">Whether the provider can read the format into a model.</param>
        /// <param name="canWrite">Whether the provider can write a model to the format.</param>
        /// <param name="roundTrips">
        /// Whether reading and then writing reproduces the source without loss.
        /// </param>
        /// <param name="fidelityNote">A human-readable note describing mapping fidelity.</param>
        public FormatCapabilities(bool canRead, bool canWrite, bool roundTrips, string fidelityNote)
        {
            this.CanRead = canRead;
            this.CanWrite = canWrite;
            this.RoundTrips = roundTrips;
            this.FidelityNote = fidelityNote ?? throw new ArgumentNullException(nameof(fidelityNote));
        }

        /// <summary>
        /// Gets a value indicating whether the provider can read the format into a model.
        /// </summary>
        public bool CanRead { get; }

        /// <summary>
        /// Gets a value indicating whether the provider can write a model to the format.
        /// </summary>
        public bool CanWrite { get; }

        /// <summary>
        /// Gets a value indicating whether reading and then writing reproduces the source
        /// document without loss.
        /// </summary>
        public bool RoundTrips { get; }

        /// <summary>
        /// Gets a human-readable note describing how faithfully the format maps to and from the
        /// canonical threat model.
        /// </summary>
        public string FidelityNote { get; }
    }
}
