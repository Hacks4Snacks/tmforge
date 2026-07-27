namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for <see cref="McpPathPolicy"/>'s rejection paths — the branches that exist only to
    /// stop hostile input. <c>McpToolsTest</c> covers the policy through the tool layer (traversal,
    /// symlink escapes, expansion bombs, oversized files); these drive the policy directly, because
    /// the ZIP container pre-flight is a hand-rolled parser over attacker-controlled bytes and each
    /// of its refusals needs a fixture that differs from a well-formed package by exactly one field.
    /// </summary>
    [TestClass]
    public class McpPathPolicyTest
    {
        /// <summary>The fixed prelude standing in for a local file header and its data.</summary>
        private const int PreludeBytes = 32;

        /// <summary>Gets or sets the workspace root created for each test.</summary>
        private string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>Creates an isolated workspace root for the test.</summary>
        [TestInitialize]
        public void Initialize()
        {
            this.WorkingDirectory = Path.Join(Path.GetTempPath(), "tmforge-policy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.WorkingDirectory);
        }

        /// <summary>Removes the workspace root after the test.</summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(this.WorkingDirectory))
            {
                Directory.Delete(this.WorkingDirectory, recursive: true);
            }
        }

        /// <summary>
        /// Verifies the fixture builder itself produces a container the policy accepts. Without this
        /// every rejection below could pass for the wrong reason — a malformed baseline would throw
        /// no matter which field the test corrupted.
        /// </summary>
        [TestMethod]
        public void ValidateArchiveContainer_AcceptsTheWellFormedFixture()
        {
            McpPathPolicy policy = this.CreatePolicy();

            policy.ValidateArchiveContainer(Package());
            policy.ValidateArchiveContainer(Package(entryCount: 3));
        }

        /// <summary>Verifies that content which opens like a ZIP but carries no end record is refused.</summary>
        [TestMethod]
        public void ValidateArchiveContainer_RejectsMissingEndRecord()
        {
            byte[] content = new byte[64];
            PutUInt32(content, 0, 0x04034b50);
            McpPathPolicy policy = this.CreatePolicy();

            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => policy.ValidateArchiveContainer(content));
            StringAssert.Contains(error.Message, "end record");
        }

        /// <summary>
        /// Verifies that the ZIP64 sentinels are refused rather than silently truncated to their
        /// 32-bit values, which is how a ZIP64 package would otherwise smuggle past the size limits.
        /// </summary>
        /// <param name="offset">The end-record field offset to fill with the sentinel.</param>
        /// <param name="width">The width of that field in bytes.</param>
        [TestMethod]
        [DataRow(10, 2, DisplayName = "total entries = 0xFFFF")]
        [DataRow(12, 4, DisplayName = "directory size = 0xFFFFFFFF")]
        [DataRow(16, 4, DisplayName = "directory offset = 0xFFFFFFFF")]
        public void ValidateArchiveContainer_RejectsZip64Sentinels(int offset, int width)
        {
            byte[] content = Package();
            int end = EndRecord(content);
            if (width == 2)
            {
                PutUInt16(content, end + offset, ushort.MaxValue);
            }
            else
            {
                PutUInt32(content, end + offset, uint.MaxValue);
            }

            McpPathPolicy policy = this.CreatePolicy();

            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => policy.ValidateArchiveContainer(content));
            StringAssert.Contains(error.Message, "ZIP64");
        }

        /// <summary>Verifies that a declared entry count above the cap is refused before the directory is walked.</summary>
        [TestMethod]
        public void ValidateArchiveContainer_RejectsTooManyDeclaredEntries()
        {
            byte[] content = Package();
            PutUInt16(content, EndRecord(content) + 10, 4097);
            McpPathPolicy policy = this.CreatePolicy();

            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => policy.ValidateArchiveContainer(content));
            StringAssert.Contains(error.Message, "4096 entries");
        }

        /// <summary>Verifies that an oversized central directory is refused on its declared size alone.</summary>
        [TestMethod]
        public void ValidateArchiveContainer_RejectsOversizedCentralDirectory()
        {
            byte[] content = Package();
            PutUInt32(content, EndRecord(content) + 12, (4L * 1024 * 1024) + 1);
            McpPathPolicy policy = this.CreatePolicy();

            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => policy.ValidateArchiveContainer(content));
            StringAssert.Contains(error.Message, "central directory exceeds");
        }

        /// <summary>
        /// Verifies that a directory claiming to extend past the end record is refused. This is the
        /// bound that keeps the walk below from reading outside the buffer.
        /// </summary>
        [TestMethod]
        public void ValidateArchiveContainer_RejectsDirectoryRunningPastTheEndRecord()
        {
            byte[] content = Package();
            int end = EndRecord(content);
            PutUInt32(content, end + 16, end - 4);
            McpPathPolicy policy = this.CreatePolicy();

            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => policy.ValidateArchiveContainer(content));
            StringAssert.Contains(error.Message, "invalid central directory");
        }

        /// <summary>Verifies that a central-directory record without its signature is refused.</summary>
        [TestMethod]
        public void ValidateArchiveContainer_RejectsMalformedEntrySignature()
        {
            byte[] content = Package();
            PutUInt32(content, PreludeBytes, 0x02014b51);
            McpPathPolicy policy = this.CreatePolicy();

            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => policy.ValidateArchiveContainer(content));
            StringAssert.Contains(error.Message, "malformed central directory entry");
        }

        /// <summary>Verifies that an entry name longer than the cap is refused.</summary>
        [TestMethod]
        public void ValidateArchiveContainer_RejectsOversizedEntryName()
        {
            byte[] content = Package();
            PutUInt16(content, PreludeBytes + 28, 1025);
            McpPathPolicy policy = this.CreatePolicy();

            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => policy.ValidateArchiveContainer(content));
            StringAssert.Contains(error.Message, "entry name exceeds");
        }

        /// <summary>
        /// Verifies that an entry whose declared lengths push the cursor past the directory is refused.
        /// A record can overshoot through its extra or comment length without its name being oversized.
        /// </summary>
        [TestMethod]
        public void ValidateArchiveContainer_RejectsEntryRunningPastTheDirectory()
        {
            byte[] content = Package();
            PutUInt16(content, PreludeBytes + 30, 1000);
            McpPathPolicy policy = this.CreatePolicy();

            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => policy.ValidateArchiveContainer(content));
            StringAssert.Contains(error.Message, "entry length");
        }

        /// <summary>
        /// Verifies that a directory holding more records than the end record declares is refused.
        /// Trusting the count would leave the trailing records unvalidated.
        /// </summary>
        [TestMethod]
        public void ValidateArchiveContainer_RejectsCountBelowDirectoryContent()
        {
            byte[] content = Package(entryCount: 2);
            PutUInt16(content, EndRecord(content) + 10, 1);
            McpPathPolicy policy = this.CreatePolicy();

            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => policy.ValidateArchiveContainer(content));
            StringAssert.Contains(error.Message, "does not match");
        }

        /// <summary>Verifies that content which is not a ZIP at all passes straight through.</summary>
        [TestMethod]
        public void ValidateArchiveContainer_IgnoresContentThatIsNotAnArchive()
        {
            McpPathPolicy policy = this.CreatePolicy();

            policy.ValidateArchiveContainer(Encoding.UTF8.GetBytes("<ThreatModel/>"));
            policy.ValidateArchiveContainer(Array.Empty<byte>());
        }

        /// <summary>
        /// Verifies that an entry whose declared expanded size alone blows the read budget is refused
        /// before any of it is decompressed.
        /// </summary>
        [TestMethod]
        public void ValidateExpandedContent_RejectsEntryLargerThanTheRemainingBudget()
        {
            byte[] package = VisioPackage(4096);
            McpPathPolicy policy = this.CreatePolicy(maxReadBytes: 1024);

            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => policy.ValidateExpandedContent(package, "vsdx"));
            StringAssert.Contains(error.Message, "Expanded Visio package");
        }

        /// <summary>Verifies that the expansion budget is only applied to the format that carries an archive.</summary>
        [TestMethod]
        public void ValidateExpandedContent_IgnoresNonArchiveFormats()
        {
            byte[] package = VisioPackage(4096);
            McpPathPolicy policy = this.CreatePolicy(maxReadBytes: 1024);

            policy.ValidateExpandedContent(package, "tm7");
        }

        /// <summary>Verifies that an empty workspace root is refused at construction.</summary>
        [TestMethod]
        public void Constructor_RejectsAnEmptyRoot()
        {
            _ = Assert.Throws<ArgumentException>(() => new McpPathPolicy("   "));
        }

        /// <summary>Verifies that a workspace root that does not exist is refused at construction.</summary>
        [TestMethod]
        public void Constructor_RejectsAMissingRoot()
        {
            string missing = Path.Join(this.WorkingDirectory, "no-such-directory");

            _ = Assert.Throws<DirectoryNotFoundException>(() => new McpPathPolicy(missing));
        }

        /// <summary>
        /// Verifies that a file cannot stand in for the workspace root. The refusal comes from the
        /// directory check in the constructor, before any path is ever resolved against it.
        /// </summary>
        [TestMethod]
        public void Constructor_RejectsARootThatIsAFile()
        {
            string file = Path.Join(this.WorkingDirectory, "root.txt");
            File.WriteAllText(file, "x");

            _ = Assert.Throws<DirectoryNotFoundException>(() => new McpPathPolicy(file));
        }

        /// <summary>
        /// Verifies that the read and write limits must be positive and fit an <see cref="int"/>.
        /// A non-positive or overflowing limit would disable the bound it exists to enforce.
        /// </summary>
        /// <param name="limit">The candidate limit.</param>
        [TestMethod]
        [DataRow(0L, DisplayName = "zero")]
        [DataRow(-1L, DisplayName = "negative")]
        [DataRow((long)int.MaxValue + 1, DisplayName = "beyond Int32")]
        public void Constructor_RejectsLimitsOutsideTheSupportedRange(long limit)
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(
                () => new McpPathPolicy(this.WorkingDirectory, maxReadBytes: limit));
            _ = Assert.Throws<ArgumentOutOfRangeException>(
                () => new McpPathPolicy(this.WorkingDirectory, maxWriteBytes: limit));
        }

        /// <summary>Verifies that an empty path is refused rather than resolving to the workspace root.</summary>
        [TestMethod]
        public void ReadAllBytes_RejectsAnEmptyPath()
        {
            McpPathPolicy policy = this.CreatePolicy();

            _ = Assert.Throws<ArgumentException>(() => policy.ReadAllBytes("  "));
        }

        /// <summary>
        /// Verifies that a missing file inside the root is reported as missing rather than as an escape,
        /// so an agent gets an actionable error instead of a security refusal. The refusal comes from
        /// path resolution, which walks every component before the file itself is opened.
        /// </summary>
        [TestMethod]
        public void ReadAllBytes_RejectsAMissingFile()
        {
            McpPathPolicy policy = this.CreatePolicy();

            _ = Assert.Throws<FileNotFoundException>(() => policy.ReadAllBytes("absent.tm7"));
        }

        /// <summary>Verifies that a file larger than the read limit is refused on its length alone.</summary>
        [TestMethod]
        public void ReadAllBytes_RejectsAFileOverTheReadLimit()
        {
            File.WriteAllBytes(Path.Join(this.WorkingDirectory, "big.tm7"), new byte[2048]);
            McpPathPolicy policy = this.CreatePolicy(maxReadBytes: 1024);

            IOException error = Assert.Throws<IOException>(() => policy.ReadAllBytes("big.tm7"));
            StringAssert.Contains(error.Message, "read limit");
        }

        /// <summary>
        /// Verifies that a file cannot be used as a directory component. Without this a path like
        /// <c>model.tm7/../../etc</c> could be walked through a non-directory on some platforms.
        /// </summary>
        [TestMethod]
        public void ReadAllBytes_RejectsANonDirectoryPathComponent()
        {
            File.WriteAllText(Path.Join(this.WorkingDirectory, "model.tm7"), "x");
            McpPathPolicy policy = this.CreatePolicy();

            _ = Assert.Throws<DirectoryNotFoundException>(
                () => policy.ReadAllBytes(Path.Join("model.tm7", "child.tm7")));
        }

        /// <summary>
        /// Verifies that a symbolic link whose target does not exist never yields content.
        /// <para>
        /// The link is created against <see cref="McpPathPolicy.RootPath"/> rather than the raw temp
        /// path on purpose. macOS hands out <c>/var/folders/...</c> while the canonical root is
        /// <c>/private/var/folders/...</c>, so a link built from the raw path is refused for being
        /// outside the root — which would make this pass without ever exercising a dangling link.
        /// </para>
        /// <para>
        /// The exception type is not pinned because the platforms disagree on how a dangling link
        /// resolves: .NET on macOS returns a target that simply does not exist (reported as missing),
        /// while a runtime that returns no target at all reports a refusal. Both satisfy the property
        /// that matters here — the read fails and no content is produced.
        /// </para>
        /// </summary>
        [TestMethod]
        public void ReadAllBytes_NeverFollowsADanglingSymbolicLink()
        {
            McpPathPolicy policy = this.CreatePolicy();
            string link = Path.Join(policy.RootPath, "dangling.tm7");
            File.CreateSymbolicLink(link, Path.Join(policy.RootPath, "never-created.tm7"));

            try
            {
                _ = policy.ReadAllBytes("dangling.tm7");
                Assert.Fail("A dangling symbolic link must not produce content.");
            }
            catch (FileNotFoundException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>
        /// Verifies that a symbolic link pointing outside the workspace is refused. This is the escape
        /// the containment check exists for, and unlike the dangling case it is the same everywhere.
        /// </summary>
        [TestMethod]
        public void ReadAllBytes_RejectsASymbolicLinkOutOfTheWorkspace()
        {
            string outside = Path.Join(Path.GetTempPath(), "tmforge-outside-" + Guid.NewGuid().ToString("N") + ".tm7");
            File.WriteAllText(outside, "x");
            McpPathPolicy policy = this.CreatePolicy();
            File.CreateSymbolicLink(Path.Join(policy.RootPath, "escape.tm7"), outside);

            try
            {
                UnauthorizedAccessException error = Assert.Throws<UnauthorizedAccessException>(
                    () => policy.ReadAllBytes("escape.tm7"));
                StringAssert.Contains(error.Message, "confined to the configured workspace root");
            }
            finally
            {
                File.Delete(outside);
            }
        }

        /// <summary>
        /// Verifies that the policy refuses to operate once its root has been removed, rather than
        /// resolving paths against a directory that no longer exists.
        /// </summary>
        [TestMethod]
        public void ReadAllBytes_RejectsWorkFromARootThatHasBeenRemoved()
        {
            McpPathPolicy policy = this.CreatePolicy();
            Directory.Delete(this.WorkingDirectory, recursive: true);

            UnauthorizedAccessException error = Assert.Throws<UnauthorizedAccessException>(
                () => policy.ReadAllBytes("model.tm7"));
            StringAssert.Contains(error.Message, "no longer available");
        }

        /// <summary>Verifies that an empty destination path is refused before a format is chosen for it.</summary>
        [TestMethod]
        public void ResolveWriteTarget_RejectsAnEmptyPath()
        {
            McpPathPolicy policy = this.CreatePolicy();

            _ = Assert.Throws<ArgumentException>(() => policy.ResolveWriteTarget("   ", formatId: null));
        }

        /// <summary>
        /// Verifies that a destination without a registered model extension is refused, so the write
        /// tools cannot be used to drop arbitrary files into the workspace.
        /// </summary>
        [TestMethod]
        public void ResolveWriteTarget_RejectsAnUnregisteredExtension()
        {
            McpPathPolicy policy = this.CreatePolicy();

            NotSupportedException error = Assert.Throws<NotSupportedException>(
                () => policy.ResolveWriteTarget("payload.sh", formatId: null));
            StringAssert.Contains(error.Message, "registered threat-model file extension");
        }

        /// <summary>Verifies that a null serializer callback is refused before a temporary file is created.</summary>
        [TestMethod]
        public void WriteAtomically_RejectsANullCallback()
        {
            McpPathPolicy policy = this.CreatePolicy();

            _ = Assert.Throws<ArgumentNullException>(() => policy.WriteAtomically("model.tm7", write: null!));
        }

        /// <summary>
        /// Verifies that the write bound holds when a serializer grows the file with
        /// <see cref="Stream.SetLength"/> instead of writing bytes.
        /// </summary>
        [TestMethod]
        public void WriteAtomically_RejectsSetLengthBeyondTheWriteLimit()
        {
            McpPathPolicy policy = this.CreatePolicy(maxWriteBytes: 64);

            IOException error = Assert.Throws<IOException>(
                () => policy.WriteAtomically("model.tm7", stream => stream.SetLength(4096)));
            StringAssert.Contains(error.Message, "write limit");
            Assert.IsFalse(File.Exists(Path.Join(this.WorkingDirectory, "model.tm7")));
        }

        /// <summary>
        /// Verifies that the write bound holds when a serializer seeks past the limit before writing,
        /// which would otherwise leave a sparse file larger than the cap.
        /// </summary>
        [TestMethod]
        public void WriteAtomically_RejectsSeekingBeyondTheWriteLimit()
        {
            McpPathPolicy policy = this.CreatePolicy(maxWriteBytes: 64);

            _ = Assert.Throws<IOException>(
                () => policy.WriteAtomically("model.tm7", stream => stream.Position = 4096));
            _ = Assert.Throws<IOException>(
                () => policy.WriteAtomically("model.tm7", stream => stream.Seek(4096, SeekOrigin.Begin)));
            Assert.IsFalse(File.Exists(Path.Join(this.WorkingDirectory, "model.tm7")));
        }

        /// <summary>Verifies that the bounded output stream is write-only, as a serializer target should be.</summary>
        [TestMethod]
        public void WriteAtomically_ExposesAWriteOnlyStream()
        {
            McpPathPolicy policy = this.CreatePolicy();

            _ = policy.WriteAtomically(
                "model.tm7",
                stream =>
                {
                    Assert.IsFalse(stream.CanRead);
                    _ = Assert.Throws<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
                    stream.Write(new byte[] { 1, 2, 3 }, 0, 3);
                });

            Assert.AreEqual(3, new FileInfo(Path.Join(this.WorkingDirectory, "model.tm7")).Length);
        }

        /// <summary>Writes a big-endian-safe unsigned 16-bit value at the given offset.</summary>
        /// <param name="content">The buffer to patch.</param>
        /// <param name="offset">The offset to write at.</param>
        /// <param name="value">The value to store.</param>
        private static void PutUInt16(byte[] content, int offset, int value)
        {
            content[offset] = (byte)(value & 0xFF);
            content[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        /// <summary>Writes an unsigned 32-bit value at the given offset.</summary>
        /// <param name="content">The buffer to patch.</param>
        /// <param name="offset">The offset to write at.</param>
        /// <param name="value">The value to store.</param>
        private static void PutUInt32(byte[] content, int offset, long value)
        {
            content[offset] = (byte)(value & 0xFF);
            content[offset + 1] = (byte)((value >> 8) & 0xFF);
            content[offset + 2] = (byte)((value >> 16) & 0xFF);
            content[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        /// <summary>Returns the offset of the end-of-central-directory record in a fixture package.</summary>
        /// <param name="content">The fixture package.</param>
        /// <returns>The end-record offset.</returns>
        private static int EndRecord(byte[] content) => content.Length - 22;

        /// <summary>
        /// Builds a structurally valid ZIP container: a local-header prelude so the content is
        /// recognized as an archive, one central-directory record per entry, and an end record. Every
        /// rejection test starts here and corrupts exactly one field.
        /// </summary>
        /// <param name="entryCount">The number of central-directory records to emit.</param>
        /// <param name="nameLength">The length of each record's entry name.</param>
        /// <returns>The fixture package.</returns>
        private static byte[] Package(int entryCount = 1, int nameLength = 5)
        {
            int recordBytes = 46 + nameLength;
            int directorySize = entryCount * recordBytes;
            byte[] content = new byte[PreludeBytes + directorySize + 22];

            // A local file header signature, so the policy treats the content as an archive.
            PutUInt32(content, 0, 0x04034b50);

            for (int index = 0; index < entryCount; index++)
            {
                int record = PreludeBytes + (index * recordBytes);
                PutUInt32(content, record, 0x02014b50);
                PutUInt16(content, record + 28, nameLength);
                PutUInt16(content, record + 30, 0);
                PutUInt16(content, record + 32, 0);
                for (int position = 0; position < nameLength; position++)
                {
                    content[record + 46 + position] = (byte)'a';
                }
            }

            int end = PreludeBytes + directorySize;
            PutUInt32(content, end, 0x06054b50);
            PutUInt16(content, end + 8, entryCount);
            PutUInt16(content, end + 10, entryCount);
            PutUInt32(content, end + 12, directorySize);
            PutUInt32(content, end + 16, PreludeBytes);
            PutUInt16(content, end + 20, 0);
            return content;
        }

        /// <summary>Builds a real (deflate) Visio package holding one highly compressible entry.</summary>
        /// <param name="expandedBytes">The uncompressed size of the payload entry.</param>
        /// <returns>The package bytes.</returns>
        private static byte[] VisioPackage(int expandedBytes)
        {
            using MemoryStream buffer = new MemoryStream();
            using (ZipArchive archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                using Stream entry = archive.CreateEntry("visio/pages/page1.xml").Open();
                entry.Write(Encoding.UTF8.GetBytes(new string('x', expandedBytes)));
            }

            return buffer.ToArray();
        }

        /// <summary>Creates a policy rooted at this test's workspace.</summary>
        /// <param name="maxReadBytes">The read limit.</param>
        /// <param name="maxWriteBytes">The write limit.</param>
        /// <returns>The policy.</returns>
        private McpPathPolicy CreatePolicy(
            long maxReadBytes = McpPathPolicy.DefaultMaxReadBytes,
            long maxWriteBytes = McpPathPolicy.DefaultMaxWriteBytes)
            => new McpPathPolicy(this.WorkingDirectory, maxReadBytes, maxWriteBytes);
    }
}
