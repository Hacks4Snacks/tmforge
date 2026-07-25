namespace ThreatModelForge.Engine
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Wraps a destination stream so a writer that owns and disposes its output cannot close the
    /// caller's stream. The engine's report writers hand their output to library writers that close
    /// what they are given, but an HTTP response body or a caller-owned file must outlive one report.
    /// </summary>
    internal sealed class NonClosingStream : Stream
    {
        private readonly Stream inner;

        /// <summary>Initializes a new instance of the <see cref="NonClosingStream"/> class.</summary>
        /// <param name="inner">The stream to write through to, which is never closed.</param>
        public NonClosingStream(Stream inner)
        {
            this.inner = inner;
        }

        /// <inheritdoc/>
        public override bool CanRead => this.inner.CanRead;

        /// <inheritdoc/>
        public override bool CanSeek => this.inner.CanSeek;

        /// <inheritdoc/>
        public override bool CanWrite => this.inner.CanWrite;

        /// <inheritdoc/>
        public override long Length => this.inner.Length;

        /// <inheritdoc/>
        public override long Position
        {
            get => this.inner.Position;
            set => this.inner.Position = value;
        }

        /// <inheritdoc/>
        public override void Flush() => this.inner.Flush();

        /// <inheritdoc/>
        public override Task FlushAsync(CancellationToken cancellationToken) =>
            this.inner.FlushAsync(cancellationToken);

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count) => this.inner.Read(buffer, offset, count);

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => this.inner.Seek(offset, origin);

        /// <inheritdoc/>
        public override void SetLength(long value) => this.inner.SetLength(value);

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count) => this.inner.Write(buffer, offset, count);

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            // Flush the buffered content, but leave the caller's stream open and positioned for them.
            if (disposing)
            {
                this.inner.Flush();
            }

            base.Dispose(disposing);
        }
    }
}
