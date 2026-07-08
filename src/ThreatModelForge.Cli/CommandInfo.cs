namespace ThreatModelForge.Cli
{
    using System;

    /// <summary>
    /// Describes a single <c>tmforge</c> verb once: its dispatch handler, its one-line usage summary,
    /// and the shape of its machine-readable <c>--json</c> <c>data</c> payload (or <see langword="null"/>
    /// when the verb has no machine-readable output). The dispatcher, <c>--help</c> usage, and
    /// <c>tmforge schema</c> are all generated from these descriptors, so adding a verb in one place
    /// keeps all three in sync.
    /// </summary>
    internal sealed class CommandInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CommandInfo"/> class.
        /// </summary>
        /// <param name="verb">The verb that selects the command (the first CLI argument).</param>
        /// <param name="summary">A one-line description shown in <c>tmforge --help</c>.</param>
        /// <param name="jsonData">The <c>--json</c> <c>data</c> payload shape, or <see langword="null"/> when the verb produces no machine-readable output.</param>
        /// <param name="run">The command handler; receives the arguments after the verb and returns the exit code.</param>
        public CommandInfo(string verb, string summary, string? jsonData, Func<string[], int> run)
        {
            this.Verb = verb ?? throw new ArgumentNullException(nameof(verb));
            this.Summary = summary ?? throw new ArgumentNullException(nameof(summary));
            this.JsonData = jsonData;
            this.Run = run ?? throw new ArgumentNullException(nameof(run));
        }

        /// <summary>Gets the verb that selects the command.</summary>
        public string Verb { get; }

        /// <summary>Gets the one-line usage summary.</summary>
        public string Summary { get; }

        /// <summary>Gets the <c>--json</c> <c>data</c> payload shape, or <see langword="null"/> when there is none.</summary>
        public string? JsonData { get; }

        /// <summary>Gets the command handler.</summary>
        public Func<string[], int> Run { get; }
    }
}
