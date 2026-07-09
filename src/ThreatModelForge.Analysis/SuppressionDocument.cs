namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text.Json;

    /// <summary>
    /// Set of suppressions organized by file.
    /// </summary>
    public class SuppressionDocument
    {
        /// <summary>
        /// The comparison used to match a threat-model path against a declared suppression path.
        /// Windows and macOS default to case-insensitive file systems, so paths are matched
        /// case-insensitively there; every other platform (Linux and other Unix file systems) is
        /// treated as case-sensitive so a suppression declared for <c>Model.tm7</c> is not applied
        /// to a distinct <c>model.tm7</c>.
        /// </summary>
        private static readonly StringComparison PathComparison =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        /// <summary>
        /// Gets or sets the set of file elements in this document.
        /// </summary>
#pragma warning disable CA1819 // Properties should not return arrays. System.Text.Json does not support deserializing read only collection properties.
#pragma warning disable SA1011 // Closing square brackets should be spaced correctly
        public FileSuppressionElement[]? Files { get; set; }
#pragma warning restore SA1011 // Closing square brackets should be spaced correctly
#pragma warning restore CA1819 // Properties should not return arrays

        /// <summary>
        /// Loads a document from json.
        /// </summary>
        /// <param name="path">The path to the suppression JSON.</param>
        /// <returns>
        /// A new instance of the <see cref="SuppressionDocument"/> class.
        /// </returns>
        public static SuppressionDocument Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentOutOfRangeException(nameof(path));
            }

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            if (!Path.IsPathRooted(path))
            {
                path = Path.GetFullPath(path);
            }

            string folder = Path.GetDirectoryName(path);

            string jsonString = File.ReadAllText(path);
            SuppressionDocument? doc = JsonSerializer.Deserialize<SuppressionDocument>(
                jsonString,
                options);
            if (doc == null)
            {
                // The JSON content could be the null keyword.
                doc = new SuppressionDocument();
            }

            foreach (FileSuppressionElement element in doc!.Files ?? Array.Empty<FileSuppressionElement>())
            {
                if (!Path.IsPathRooted(element.File))
                {
                    element.File = Path.Combine(folder, element.File);
                }
            }

            return doc;
        }

        /// <summary>
        /// Gets the list of suppressions declared for a given file.
        /// </summary>
        /// <param name="path">The path to the TM7 document.</param>
        /// <returns>The list of suppressions.</returns>
        public IEnumerable<SuppressMessage> GetSuppressions(string path)
        {
            if (!Path.IsPathRooted(path))
            {
                path = Path.GetFullPath(path);
            }

            FileSuppressionElement file =
                (this.Files ?? Array.Empty<FileSuppressionElement>())
                .FirstOrDefault(e => string.Equals(e.File, path, PathComparison));
            if (file == null)
            {
                return Array.Empty<SuppressMessage>();
            }

            return file.Suppressions ?? Array.Empty<SuppressMessage>();
        }
    }
}
