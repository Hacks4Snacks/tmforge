namespace ThreatModelForge.Formats
{
    using System.Collections.Generic;
    using System.IO;
    using ThreatModelForge.Model;

    /// <summary>
    /// A provider that reads and writes a single threat-model file format, mapping it to and from
    /// the canonical <see cref="ThreatModel"/>.
    /// </summary>
    public interface IThreatModelFormat
    {
        /// <summary>
        /// Gets the stable identifier for the format (for example, <c>tm7</c>).
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the human-readable display name for the format.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Gets the file extensions (including the leading dot) associated with the format.
        /// </summary>
        IReadOnlyList<string> Extensions { get; }

        /// <summary>
        /// Gets the capabilities of the provider.
        /// </summary>
        FormatCapabilities Capabilities { get; }

        /// <summary>
        /// Determines whether the provider can read the content at the current position of the
        /// supplied stream by inspecting a prefix of the stream. Implementations must leave the
        /// stream position unchanged; a seekable stream is required.
        /// </summary>
        /// <param name="stream">The stream to inspect.</param>
        /// <returns><see langword="true"/> if the content appears to be this format.</returns>
        bool CanRead(Stream stream);

        /// <summary>
        /// Reads a threat model from the supplied stream.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <returns>The parsed <see cref="ThreatModel"/>.</returns>
        ThreatModel Read(Stream stream);

        /// <summary>
        /// Writes a threat model to the supplied stream.
        /// </summary>
        /// <param name="model">The model to write.</param>
        /// <param name="stream">The stream to write to.</param>
        void Write(ThreatModel model, Stream stream);
    }
}
