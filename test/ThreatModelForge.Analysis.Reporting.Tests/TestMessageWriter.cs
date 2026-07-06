namespace ThreatModelForge.Analysis.Reporting.Tests
{
    using System;

    /// <summary>
    /// Writes messages to the console.
    /// </summary>
    public class TestMessageWriter : MessageWriter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestMessageWriter"/> class.
        /// </summary>
        /// <param name="path">The model path.</param>
        public TestMessageWriter(string path)
        {
            this.Path = path;
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
            string line = $"{this.Path}: {severity} {messageID}: {text}";
            if (severity == MessageSeverity.Error)
            {
                this.HasErrors = true;
                Console.Error.WriteLine(line);
            }
            else
            {
                Console.WriteLine(line);
            }
        }
    }
}
