namespace ThreatModelForge.Cli
{
    using System;
    using ThreatModelForge.Analysis;

    /// <summary>
    /// Writes messages to the console.
    /// </summary>
    internal class ConsoleMessageWriter : MessageWriter
    {
        private readonly bool quiet;
        private int mostSevereRank = int.MaxValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsoleMessageWriter"/> class.
        /// </summary>
        /// <param name="path">The model path.</param>
        /// <param name="quiet">
        /// When <see langword="true"/>, messages are recorded (so <see cref="MessageWriter.HasErrors"/>
        /// still reflects error findings) but not written to the console; used for <c>--json</c> mode.
        /// </param>
        public ConsoleMessageWriter(string path, bool quiet = false)
        {
            this.Path = path;
            this.quiet = quiet;
        }

        /// <summary>
        /// Gets the path.
        /// </summary>
        public string Path { get; }

        /// <inheritdoc />
        public override void WriteCore(
            MessageSeverity severity,
            string messageID,
            string text)
        {
            if ((int)severity < this.mostSevereRank)
            {
                this.mostSevereRank = (int)severity;
            }

            if (severity == MessageSeverity.Error)
            {
                this.HasErrors = true;
            }

            if (this.quiet)
            {
                return;
            }

            string line = $"{this.Path}: {severity} {messageID}: {text}";
            if (severity == MessageSeverity.Error)
            {
                Console.Error.WriteLine(line);
            }
            else
            {
                Console.WriteLine(line);
            }
        }

        /// <summary>
        /// Returns whether any recorded finding is at or above the given severity threshold, where
        /// <see cref="MessageSeverity.Error"/> is the most severe. Used to gate the analyze exit code.
        /// </summary>
        /// <param name="threshold">The minimum severity that should gate the exit code.</param>
        /// <returns><see langword="true"/> when a finding met or exceeded the threshold.</returns>
        public bool MeetsThreshold(MessageSeverity threshold) => this.mostSevereRank <= (int)threshold;
    }
}
