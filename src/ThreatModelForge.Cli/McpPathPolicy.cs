namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using ThreatModelForge.Formats;

    /// <summary>
    /// Confines MCP file access to one canonical workspace root, resolves symbolic links before use,
    /// enforces bounded reads/writes, and permits writes only to registered model formats.
    /// </summary>
    internal sealed class McpPathPolicy
    {
        /// <summary>The default maximum number of bytes read from one model file.</summary>
        public const long DefaultMaxReadBytes = 64L * 1024 * 1024;

        /// <summary>The default maximum number of bytes written to one model file.</summary>
        public const long DefaultMaxWriteBytes = 64L * 1024 * 1024;

        private const int MaxArchiveEntries = 4096;

        private const int MaxArchiveEntryNameBytes = 1024;

        private const long MaxCentralDirectoryBytes = 4L * 1024 * 1024;

        private readonly StringComparison pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpPathPolicy"/> class.
        /// </summary>
        /// <param name="rootPath">The directory that contains every path the MCP server may access.</param>
        /// <param name="maxReadBytes">The maximum size of one input file.</param>
        /// <param name="maxWriteBytes">The maximum size of one output file.</param>
        public McpPathPolicy(
            string rootPath,
            long maxReadBytes = DefaultMaxReadBytes,
            long maxWriteBytes = DefaultMaxWriteBytes)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                throw new ArgumentException("The MCP workspace root cannot be empty.", nameof(rootPath));
            }

            ValidateLimit(maxReadBytes, nameof(maxReadBytes));
            ValidateLimit(maxWriteBytes, nameof(maxWriteBytes));

            string fullRoot = Path.GetFullPath(rootPath);
            if (!Directory.Exists(fullRoot))
            {
                throw new DirectoryNotFoundException("MCP workspace root not found: " + fullRoot);
            }

            this.RootPath = ResolveAbsoluteExistingPath(fullRoot);
            this.MaxReadBytes = maxReadBytes;
            this.MaxWriteBytes = maxWriteBytes;
        }

        /// <summary>Gets the canonical workspace root.</summary>
        public string RootPath { get; }

        /// <summary>Gets the maximum size of one input file.</summary>
        public long MaxReadBytes { get; }

        /// <summary>Gets the maximum size of one output file.</summary>
        public long MaxWriteBytes { get; }

        /// <summary>Reads a file inside the workspace root, rejecting escapes and oversized input.</summary>
        /// <param name="path">The caller-supplied relative or absolute path.</param>
        /// <returns>The file contents.</returns>
        public byte[] ReadAllBytes(string path)
        {
            string resolved = this.ResolvePath(path, allowMissingLeaf: false);
            if (!File.Exists(resolved))
            {
                throw new FileNotFoundException("Model file not found inside the MCP workspace.", path);
            }

            string revalidated = this.ResolvePath(path, allowMissingLeaf: false);
            if (!string.Equals(resolved, revalidated, this.pathComparison))
            {
                throw new UnauthorizedAccessException("The MCP file path changed while it was being validated.");
            }

            using FileStream input = new FileStream(resolved, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length > this.MaxReadBytes)
            {
                throw new IOException($"Model file exceeds the MCP read limit of {this.MaxReadBytes} bytes.");
            }

            using MemoryStream output = new MemoryStream((int)input.Length);
            byte[] buffer = new byte[81920];
            long total = 0;
            int count;
            while ((count = input.Read(buffer.AsSpan())) > 0)
            {
                total += count;
                if (total > this.MaxReadBytes)
                {
                    throw new IOException($"Model file exceeds the MCP read limit of {this.MaxReadBytes} bytes.");
                }

                output.Write(buffer.AsSpan(0, count));
            }

            return output.ToArray();
        }

        /// <summary>Rejects compressed model content whose expanded size exceeds the read budget.</summary>
        /// <param name="content">The bounded on-disk content.</param>
        /// <param name="formatId">The detected or explicit format id.</param>
        public void ValidateExpandedContent(byte[] content, string? formatId)
        {
            if (!string.Equals(formatId, VisioFormat.FormatId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            this.ValidateArchiveContainer(content);
            using MemoryStream input = new MemoryStream(content, writable: false);
            using ZipArchive archive = new ZipArchive(input, ZipArchiveMode.Read);
            if (archive.Entries.Count > MaxArchiveEntries)
            {
                throw new InvalidDataException($"Visio package exceeds the MCP limit of {MaxArchiveEntries} entries.");
            }

            long expandedBytes = 0;
            byte[] buffer = new byte[81920];
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (entry.Length > this.MaxReadBytes - expandedBytes)
                {
                    throw new InvalidDataException($"Expanded Visio package exceeds the MCP read limit of {this.MaxReadBytes} bytes.");
                }

                using Stream entryStream = entry.Open();
                int count;
                while ((count = entryStream.Read(buffer.AsSpan())) > 0)
                {
                    if (count > this.MaxReadBytes - expandedBytes)
                    {
                        throw new InvalidDataException($"Expanded Visio package exceeds the MCP read limit of {this.MaxReadBytes} bytes.");
                    }

                    expandedBytes += count;
                }
            }
        }

        /// <summary>Preflights ZIP metadata before a parser can materialize its central directory.</summary>
        /// <param name="content">The bounded on-disk content.</param>
        public void ValidateArchiveContainer(byte[] content)
        {
            int endRecord = TryFindEndOfCentralDirectory(content);
            if (!LooksLikeZip(content))
            {
                if (endRecord >= 0)
                {
                    throw new InvalidDataException("Prefixed ZIP model packages are not accepted by the MCP file tools.");
                }

                return;
            }

            if (endRecord < 0)
            {
                throw new InvalidDataException("Model package is missing a valid ZIP end record.");
            }

            int entries = ReadUInt16(content, endRecord + 10);
            long directorySize = ReadUInt32(content, endRecord + 12);
            long directoryOffset = ReadUInt32(content, endRecord + 16);
            if (entries == ushort.MaxValue || directorySize == uint.MaxValue || directoryOffset == uint.MaxValue)
            {
                throw new InvalidDataException("ZIP64 model packages are not accepted by the MCP file tools.");
            }

            if (entries > MaxArchiveEntries)
            {
                throw new InvalidDataException($"Model package exceeds the MCP limit of {MaxArchiveEntries} entries.");
            }

            if (directorySize > MaxCentralDirectoryBytes || directorySize > this.MaxReadBytes)
            {
                throw new InvalidDataException($"Model package central directory exceeds the MCP limit of {MaxCentralDirectoryBytes} bytes.");
            }

            if (directoryOffset < 0 || directorySize < 0 || directoryOffset + directorySize > endRecord)
            {
                throw new InvalidDataException("Model package has an invalid central directory.");
            }

            int cursor = checked((int)directoryOffset);
            int directoryEnd = checked((int)(directoryOffset + directorySize));
            for (int index = 0; index < entries; index++)
            {
                if (cursor > directoryEnd - 46 || ReadUInt32(content, cursor) != 0x02014b50)
                {
                    throw new InvalidDataException("Model package has a malformed central directory entry.");
                }

                int nameLength = ReadUInt16(content, cursor + 28);
                int extraLength = ReadUInt16(content, cursor + 30);
                int commentLength = ReadUInt16(content, cursor + 32);
                if (nameLength > MaxArchiveEntryNameBytes)
                {
                    throw new InvalidDataException($"Model package entry name exceeds the MCP limit of {MaxArchiveEntryNameBytes} bytes.");
                }

                cursor = checked(cursor + 46 + nameLength + extraLength + commentLength);
                if (cursor > directoryEnd)
                {
                    throw new InvalidDataException("Model package has a malformed central directory entry length.");
                }
            }

            if (cursor != directoryEnd)
            {
                throw new InvalidDataException("Model package central-directory count does not match its content.");
            }
        }

        /// <summary>
        /// Resolves and validates a destination path and format before the engine materializes output.
        /// </summary>
        /// <param name="path">The caller-supplied destination path.</param>
        /// <param name="formatId">An optional explicit format id.</param>
        /// <returns>The canonical destination path and format id.</returns>
        public (string Path, string FormatId) ResolveWriteTarget(string path, string? formatId)
        {
            IThreatModelFormat format = ResolveWritableFormat(path, formatId);
            string resolved = this.ResolvePath(path, allowMissingLeaf: true);
            IThreatModelFormat resolvedFormat = ResolveWritableFormat(resolved, format.Id);
            return (resolved, resolvedFormat.Id);
        }

        /// <summary>Writes bounded content atomically to a validated path inside the workspace root.</summary>
        /// <param name="path">The canonical destination path.</param>
        /// <param name="write">The callback that serializes content into the bounded stream.</param>
        /// <returns>The number of bytes written.</returns>
        public int WriteAtomically(string path, Action<Stream> write)
        {
            if (write == null)
            {
                throw new ArgumentNullException(nameof(write));
            }

            string resolved = this.ResolvePath(path, allowMissingLeaf: true);
            string? directory = Path.GetDirectoryName(resolved);
            if (string.IsNullOrEmpty(directory))
            {
                throw new IOException("Could not determine the MCP output directory.");
            }

            string temporary = Path.Combine(directory!, ".tmforge-" + Guid.NewGuid().ToString("N") + ".tmp");
            long bytesWritten = 0;
            try
            {
                using (FileStream file = new FileStream(
                    temporary,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    FileOptions.WriteThrough))
                {
                    using BoundedWriteStream output = new BoundedWriteStream(file, this.MaxWriteBytes, leaveOpen: true);
                    write(output);
                    output.Flush();
                    bytesWritten = output.Length;
                    file.Flush(flushToDisk: true);
                }

                string revalidated = this.ResolvePath(path, allowMissingLeaf: true);
                if (!string.Equals(resolved, revalidated, this.pathComparison))
                {
                    throw new UnauthorizedAccessException("The MCP output path changed while it was being validated.");
                }

                File.Move(temporary, resolved, overwrite: true);
                return checked((int)bytesWritten);
            }
            finally
            {
                File.Delete(temporary);
            }
        }

        private static void ValidateLimit(long value, string parameterName)
        {
            if (value <= 0 || value > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(parameterName, "MCP file limits must be between 1 and Int32.MaxValue bytes.");
            }
        }

        private static bool LooksLikeZip(byte[] content)
        {
            return content != null && content.Length >= 4 &&
                (ReadUInt32(content, 0) == 0x04034b50 || ReadUInt32(content, 0) == 0x06054b50);
        }

        private static int TryFindEndOfCentralDirectory(byte[] content)
        {
            if (content == null || content.Length < 22)
            {
                return -1;
            }

            int first = Math.Max(0, content.Length - (ushort.MaxValue + 22));
            for (int offset = content.Length - 22; offset >= first; offset--)
            {
                if (ReadUInt32(content, offset) == 0x06054b50)
                {
                    int commentLength = ReadUInt16(content, offset + 20);
                    if (offset + 22 + commentLength == content.Length)
                    {
                        return offset;
                    }
                }
            }

            return -1;
        }

        private static int ReadUInt16(byte[] content, int offset)
        {
            if (offset < 0 || offset > content.Length - 2)
            {
                throw new InvalidDataException("Model package contains a truncated ZIP field.");
            }

            return content[offset] | (content[offset + 1] << 8);
        }

        private static long ReadUInt32(byte[] content, int offset)
        {
            if (offset < 0 || offset > content.Length - 4)
            {
                throw new InvalidDataException("Model package contains a truncated ZIP field.");
            }

            return (uint)(content[offset] |
                (content[offset + 1] << 8) |
                (content[offset + 2] << 16) |
                (content[offset + 3] << 24));
        }

        private static string ResolveAbsoluteExistingPath(string fullPath)
        {
            string? root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root))
            {
                throw new IOException("Could not determine the filesystem root for the MCP workspace.");
            }

            string current = Path.GetFullPath(root!);
            string relative = Path.GetRelativePath(current, fullPath);
            foreach (string segment in Split(relative))
            {
                string next = Path.Combine(current, segment);
                current = ResolveExistingComponent(next);
            }

            if (!Directory.Exists(current))
            {
                throw new DirectoryNotFoundException("MCP workspace root is not a directory: " + fullPath);
            }

            return Path.GetFullPath(current);
        }

        private static string ResolveExistingComponent(string path)
        {
            FileSystemInfo? link = LinkAt(path);
            if (link != null)
            {
                FileSystemInfo? target = link.ResolveLinkTarget(returnFinalTarget: true);
                if (target == null)
                {
                    throw new UnauthorizedAccessException("MCP paths cannot traverse a dangling symbolic link.");
                }

                return Path.GetFullPath(target.FullName);
            }

            if (!File.Exists(path) && !Directory.Exists(path))
            {
                throw new FileNotFoundException("Path component not found inside the MCP workspace.", path);
            }

            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new UnauthorizedAccessException("MCP paths cannot traverse an unresolved reparse point.");
            }

            return Path.GetFullPath(path);
        }

        private static FileSystemInfo? LinkAt(string path)
        {
            DirectoryInfo directory = new DirectoryInfo(path);
            if (directory.LinkTarget != null)
            {
                return directory;
            }

            FileInfo file = new FileInfo(path);
            return file.LinkTarget == null ? null : file;
        }

        private static IReadOnlyList<string> Split(string relativePath)
        {
            if (relativePath == ".")
            {
                return Array.Empty<string>();
            }

            return relativePath.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
        }

        private static IThreatModelFormat ResolveWritableFormat(string path, string? formatId)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("The MCP file path cannot be empty.", nameof(path));
            }

            ThreatModelFormatRegistry registry = ThreatModelFormatRegistry.CreateDefault();
            List<IThreatModelFormat> extensionMatches = registry.Formats
                .Where(format => format.Capabilities.CanWrite && MatchesExtension(path, format.Extensions))
                .ToList();
            if (extensionMatches.Count == 0)
            {
                throw new NotSupportedException("MCP writes require a registered threat-model file extension.");
            }

            if (string.IsNullOrWhiteSpace(formatId))
            {
                return extensionMatches[0];
            }

            IThreatModelFormat requested = registry.FindById(formatId!)
                ?? throw new NotSupportedException($"No threat model format with id '{formatId}'.");
            if (!requested.Capabilities.CanWrite || !extensionMatches.Any(match => string.Equals(match.Id, requested.Id, StringComparison.OrdinalIgnoreCase)))
            {
                throw new NotSupportedException($"Format '{requested.Id}' does not match the destination file extension.");
            }

            return requested;
        }

        private static bool MatchesExtension(string path, IReadOnlyList<string> extensions)
        {
            foreach (string extension in extensions)
            {
                if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private string ResolvePath(string path, bool allowMissingLeaf)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("The MCP file path cannot be empty.", nameof(path));
            }

            this.EnsureRootUnchanged();

            string fullPath = Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(path, this.RootPath);
            this.EnsureInsideRoot(fullPath);

            string current = this.RootPath;
            IReadOnlyList<string> segments = Split(Path.GetRelativePath(this.RootPath, fullPath));
            for (int index = 0; index < segments.Count; index++)
            {
                string next = Path.Combine(current, segments[index]);
                bool last = index == segments.Count - 1;
                if (!File.Exists(next) && !Directory.Exists(next) && LinkAt(next) == null)
                {
                    if (!allowMissingLeaf || !last)
                    {
                        throw new FileNotFoundException("Path component not found inside the MCP workspace.", path);
                    }

                    current = Path.GetFullPath(next);
                }
                else
                {
                    current = ResolveExistingComponent(next);
                }

                this.EnsureInsideRoot(current);
                if (!last && !Directory.Exists(current))
                {
                    throw new DirectoryNotFoundException("A non-directory path component was used inside the MCP workspace.");
                }
            }

            return Path.GetFullPath(current);
        }

        private void EnsureRootUnchanged()
        {
            if (!Directory.Exists(this.RootPath))
            {
                throw new UnauthorizedAccessException("The configured MCP workspace root is no longer available.");
            }

            string resolved = ResolveAbsoluteExistingPath(this.RootPath);
            if (!string.Equals(resolved, this.RootPath, this.pathComparison))
            {
                throw new UnauthorizedAccessException("The configured MCP workspace root was replaced by a symbolic link or reparse point.");
            }
        }

        private void EnsureInsideRoot(string candidate)
        {
            string fullCandidate = Path.GetFullPath(candidate);
            if (string.Equals(fullCandidate, this.RootPath, this.pathComparison))
            {
                return;
            }

            string prefix = Path.EndsInDirectorySeparator(this.RootPath)
                ? this.RootPath
                : this.RootPath + Path.DirectorySeparatorChar;
            if (!fullCandidate.StartsWith(prefix, this.pathComparison))
            {
                throw new UnauthorizedAccessException("MCP file access is confined to the configured workspace root.");
            }
        }

        private sealed class BoundedWriteStream : Stream
        {
            private readonly Stream inner;
            private readonly bool leaveOpen;
            private readonly long limit;

            public BoundedWriteStream(Stream inner, long limit, bool leaveOpen)
            {
                this.inner = inner;
                this.limit = limit;
                this.leaveOpen = leaveOpen;
            }

            public override bool CanRead => false;

            public override bool CanSeek => this.inner.CanSeek;

            public override bool CanWrite => this.inner.CanWrite;

            public override long Length => this.inner.Length;

            public override long Position
            {
                get => this.inner.Position;
                set
                {
                    this.EnsureWithinLimit(value);
                    this.inner.Position = value;
                }
            }

            public override void Flush() => this.inner.Flush();

            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public override long Seek(long offset, SeekOrigin origin)
            {
                long position = this.inner.Seek(offset, origin);
                this.EnsureWithinLimit(position);
                return position;
            }

            public override void SetLength(long value)
            {
                this.EnsureWithinLimit(value);
                this.inner.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                this.EnsureWithinLimit(checked(this.Position + count));
                this.inner.Write(buffer, offset, count);
            }

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                this.EnsureWithinLimit(checked(this.Position + buffer.Length));
                this.inner.Write(buffer);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && !this.leaveOpen)
                {
                    this.inner.Dispose();
                }

                base.Dispose(disposing);
            }

            private void EnsureWithinLimit(long value)
            {
                if (value > this.limit)
                {
                    throw new IOException($"Model output exceeds the MCP write limit of {this.limit} bytes.");
                }
            }
        }
    }
}
