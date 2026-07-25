namespace ThreatModelForge.Analysis
{
    using System;
    using System.IO;
    using System.Text;

    /// <summary>
    /// A declarative rule document supplied as content rather than as a filesystem path. Hosts that
    /// cannot resolve paths — the HTTP API, the in-browser WebAssembly engine, and any caller that
    /// already holds validated rule JSON — pass content through this type, so the analysis layer never
    /// depends on the filesystem while still loading exactly the same rules as the CLI.
    /// </summary>
    public sealed class RuleContent
    {
        private readonly byte[] content;

        private RuleContent(string name, byte[] content)
        {
            this.Name = name;
            this.content = content;
        }

        /// <summary>
        /// Gets the logical origin reported in diagnostics (for example, <c>corporate.tmrules.json</c>).
        /// It is never resolved against the filesystem.
        /// </summary>
        public string Name { get; }

        /// <summary>Gets the content length in bytes.</summary>
        public int Length => this.content.Length;

        /// <summary>Creates rule content from raw document bytes.</summary>
        /// <param name="name">The logical origin reported in diagnostics.</param>
        /// <param name="content">The rule document bytes. The array is copied.</param>
        /// <returns>The rule content.</returns>
        public static RuleContent FromBytes(string name, byte[] content)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));
            }

            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            byte[] copy = new byte[content.Length];
            Array.Copy(content, copy, content.Length);
            return new RuleContent(name, copy);
        }

        /// <summary>Creates rule content from a JSON document.</summary>
        /// <param name="name">The logical origin reported in diagnostics.</param>
        /// <param name="json">The rule document JSON.</param>
        /// <returns>The rule content.</returns>
        public static RuleContent FromJson(string name, string json)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));
            }

            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            return new RuleContent(name, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json));
        }

        /// <summary>
        /// Decodes this content as JSON text, so a host can forward it across a text transport (HTTP or
        /// the JavaScript boundary). Canonical UTF-8 content round-trips byte for byte and therefore
        /// keeps its pack fingerprint.
        /// </summary>
        /// <returns>The document JSON.</returns>
        public string ToJson() => DecodeJson(this.content);

        /// <summary>
        /// Decodes rule document bytes as JSON text, honoring a UTF-8, UTF-16, or UTF-32 byte-order
        /// mark. Rule documents are Unicode JSON; anything else is rejected rather than mojibaked.
        /// </summary>
        /// <param name="content">The document bytes.</param>
        /// <returns>The decoded JSON.</returns>
        internal static string DecodeJson(byte[] content)
        {
            Encoding encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            int offset = 0;
            if (HasPrefix(content, 0x00, 0x00, 0xFE, 0xFF))
            {
                encoding = new UTF32Encoding(bigEndian: true, byteOrderMark: true, throwOnInvalidCharacters: true);
                offset = 4;
            }
            else if (HasPrefix(content, 0xFF, 0xFE, 0x00, 0x00))
            {
                encoding = new UTF32Encoding(bigEndian: false, byteOrderMark: true, throwOnInvalidCharacters: true);
                offset = 4;
            }
            else if (HasPrefix(content, 0xEF, 0xBB, 0xBF))
            {
                offset = 3;
            }
            else if (HasPrefix(content, 0xFE, 0xFF))
            {
                encoding = new UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: true);
                offset = 2;
            }
            else if (HasPrefix(content, 0xFF, 0xFE))
            {
                encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true);
                offset = 2;
            }

            try
            {
                return encoding.GetString(content, offset, content.Length - offset);
            }
            catch (DecoderFallbackException ex)
            {
                throw new InvalidDataException("Rule files must contain valid Unicode JSON.", ex);
            }
        }

        /// <summary>Gets the document bytes, which the loader parses and fingerprints.</summary>
        /// <returns>The document bytes.</returns>
        internal byte[] Bytes() => this.content;

        private static bool HasPrefix(byte[] content, params byte[] prefix)
        {
            if (content.Length < prefix.Length)
            {
                return false;
            }

            for (int index = 0; index < prefix.Length; index++)
            {
                if (content[index] != prefix[index])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
