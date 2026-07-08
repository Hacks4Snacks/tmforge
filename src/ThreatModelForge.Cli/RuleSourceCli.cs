namespace ThreatModelForge.Cli
{
    using System;
    using ThreatModelForge.Analysis;

    /// <summary>
    /// Builds <see cref="RuleSourceOptions"/> from the CLI <c>--rules</c> option, wiring load and
    /// validation warnings to standard error so they never corrupt machine-readable output on standard
    /// out. Shared by the commands that load rules (<c>analyze</c>, <c>threats</c>, <c>properties</c>) so
    /// custom rules behave identically across them.
    /// </summary>
    internal static class RuleSourceCli
    {
        /// <summary>The name of the CLI value option that points at declarative rule specs.</summary>
        public const string OptionName = "rules";

        /// <summary>
        /// Builds options from a single <c>--rules</c> value (a spec file or a directory of specs).
        /// </summary>
        /// <param name="specPath">The value of <c>--rules</c>, or <see langword="null"/> when omitted.</param>
        /// <returns>Options carrying the spec path (if any) and a standard-error diagnostics sink.</returns>
        public static RuleSourceOptions FromPath(string? specPath)
        {
            Action<string> diagnostics = message => Console.Error.WriteLine("warning: " + message);
            return string.IsNullOrWhiteSpace(specPath)
                ? new RuleSourceOptions(null, diagnostics)
                : new RuleSourceOptions(new[] { specPath! }, diagnostics);
        }
    }
}
