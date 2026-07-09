namespace ThreatModelForge.Formats.Tests
{
    using System.IO;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for <see cref="Tm7Format"/>.
    /// </summary>
    [TestClass]
    public class Tm7FormatTest
    {
        /// <summary>
        /// Gets or sets the test context, used to locate deployed fixtures.
        /// </summary>
        public TestContext? TestContext { get; set; }

        /// <summary>
        /// Verifies the identity, extensions, and capabilities of the provider.
        /// </summary>
        [TestMethod]
        public void IdentityAndCapabilities()
        {
            Tm7Format format = new Tm7Format();

            Assert.AreEqual("tm7", format.Id);
            CollectionAssert.Contains((System.Collections.ICollection)format.Extensions, ".tm7");
            Assert.IsTrue(format.Capabilities.CanRead);
            Assert.IsTrue(format.Capabilities.CanWrite);
            Assert.IsTrue(format.Capabilities.RoundTrips);
            Assert.IsFalse(string.IsNullOrWhiteSpace(format.Capabilities.FidelityNote));
        }

        /// <summary>
        /// Verifies that <see cref="Tm7Format.CanRead(Stream)"/> recognizes <c>.tm7</c> content.
        /// </summary>
        [TestMethod]
        public void CanReadRecognizesTm7Content()
        {
            Tm7Format format = new Tm7Format();
            using (MemoryStream stream = WriteEmptyModel(format))
            {
                Assert.IsTrue(format.CanRead(stream));
            }
        }

        /// <summary>
        /// Verifies that <see cref="Tm7Format.CanRead(Stream)"/> rejects non-<c>.tm7</c> content.
        /// </summary>
        [TestMethod]
        public void CanReadRejectsNonTm7Content()
        {
            Tm7Format format = new Tm7Format();
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes("not a threat model")))
            {
                Assert.IsFalse(format.CanRead(stream));
            }
        }

        /// <summary>
        /// Verifies that <see cref="Tm7Format.CanRead(Stream)"/> leaves the stream position
        /// unchanged so the content can subsequently be read.
        /// </summary>
        [TestMethod]
        public void CanReadPreservesStreamPosition()
        {
            Tm7Format format = new Tm7Format();
            using (MemoryStream stream = WriteEmptyModel(format))
            {
                stream.Position = 3;
                format.CanRead(stream);
                Assert.AreEqual(3, stream.Position);
            }
        }

        /// <summary>
        /// Verifies that writing an empty model and reading it back yields a usable model.
        /// </summary>
        [TestMethod]
        public void WriteThenReadRoundTripsEmptyModel()
        {
            Tm7Format format = new Tm7Format();
            using (MemoryStream stream = WriteEmptyModel(format))
            {
                ThreatModel reloaded = format.Read(stream);
                Assert.IsNotNull(reloaded);
                Assert.IsNotNull(reloaded.DrawingSurfaceList);
            }
        }

        /// <summary>
        /// Conformance test: reading a real <c>.tm7</c> document through the provider and writing
        /// it back is byte-stable, matching the canonical serializer's lossless round trip.
        /// </summary>
        [TestMethod]
        public void ReadThenWriteIsByteStable()
        {
            Assert.IsNotNull(this.TestContext!.DeploymentDirectory);
            string sourcePath = Path.Join(this.TestContext!.DeploymentDirectory!, "SampleModel.tm7");
            Tm7Format format = new Tm7Format();

            byte[] first;
            using (FileStream source = File.OpenRead(sourcePath))
            {
                ThreatModel model = format.Read(source);
                using (MemoryStream output = new MemoryStream())
                {
                    format.Write(model, output);
                    first = output.ToArray();
                }
            }

            byte[] second;
            using (MemoryStream reload = new MemoryStream(first))
            {
                ThreatModel model = format.Read(reload);
                using (MemoryStream output = new MemoryStream())
                {
                    format.Write(model, output);
                    second = output.ToArray();
                }
            }

            CollectionAssert.AreEqual(first, second);
        }

        private static MemoryStream WriteEmptyModel(Tm7Format format)
        {
            MemoryStream stream = new MemoryStream();
            format.Write(new ThreatModel(), stream);
            stream.Position = 0;
            return stream;
        }
    }
}
