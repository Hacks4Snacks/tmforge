namespace ThreatModelForge.Formats
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using ThreatModelForge.Model;

    /// <summary>
    /// The <c>.tm7</c> format provider. Wraps the byte-stable <see cref="ThreatModel"/> load and
    /// save, which round-trips Microsoft Threat Modeling Tool documents without loss.
    /// </summary>
    public sealed class Tm7Format : IThreatModelFormat
    {
        /// <summary>
        /// The stable format identifier.
        /// </summary>
        public const string FormatId = "tm7";

        private static readonly IReadOnlyList<string> SupportedExtensions = new[] { ".tm7" };

        private static readonly FormatCapabilities Tm7Capabilities = new FormatCapabilities(
            canRead: true,
            canWrite: true,
            roundTrips: true,
            fidelityNote: "Byte-stable round-trip via DataContractSerializer over the full model graph.");

        /// <inheritdoc/>
        public string Id => FormatId;

        /// <inheritdoc/>
        public string DisplayName => "Microsoft Threat Modeling Tool (.tm7)";

        /// <inheritdoc/>
        public IReadOnlyList<string> Extensions => SupportedExtensions;

        /// <inheritdoc/>
        public FormatCapabilities Capabilities => Tm7Capabilities;

        /// <inheritdoc/>
        public bool CanRead(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!stream.CanSeek)
            {
                throw new NotSupportedException("Content sniffing requires a seekable stream.");
            }

            long originalPosition = stream.Position;
            try
            {
                using (StreamReader reader = new StreamReader(
                    stream,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 1024,
                    leaveOpen: true))
                {
                    char[] buffer = new char[512];
                    int count = reader.Read(buffer, 0, buffer.Length);
                    string prefix = new string(buffer, 0, count);
                    return prefix.IndexOf("<ThreatModel", StringComparison.Ordinal) >= 0
                        && prefix.IndexOf(
                            "schemas.datacontract.org/2004/07/ThreatModeling.Model",
                            StringComparison.Ordinal) >= 0;
                }
            }
            finally
            {
                stream.Position = originalPosition;
            }
        }

        /// <inheritdoc/>
        public ThreatModel Read(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            return ThreatModel.Load(stream);
        }

        /// <inheritdoc/>
        public void Write(ThreatModel model, Stream stream)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            model.Save(stream);
        }
    }
}
