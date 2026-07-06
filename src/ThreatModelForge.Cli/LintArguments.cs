namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ThreatModelForge.Analysis;

    /// <summary>
    /// Program arguments.
    /// </summary>
    internal class LintArguments
    {
        private static readonly string[] ValueOptionNames = new[]
        {
            "ruleset",
            "suppressionFile",
            "reportFolder",
            "max-severity",
        };

        private LintArguments(
            string path,
            string ruleSetPath,
            string suppressionFilePath,
            string reportFolderPath,
            IReadOnlyDictionary<string, string> variables,
            bool json,
            MessageSeverity maxSeverity)
        {
            this.Path = !string.IsNullOrWhiteSpace(path) ?
                path :
                throw new ArgumentOutOfRangeException(nameof(path));
            this.RuleSetPath = ruleSetPath;
            this.SuppressionFilePath = suppressionFilePath;
            this.ReportFolderPath = reportFolderPath;
            this.Json = json;
            this.MaxSeverity = maxSeverity;
            if (variables == null)
            {
                throw new ArgumentNullException(nameof(variables));
            }

            foreach (string key in variables.Keys)
            {
                this.Variables[key] = variables[key];
            }
        }

        /// <summary>
        /// Gets the path to the model TM7 document.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Gets the optional rule set path.
        /// </summary>
        public string RuleSetPath { get; }

        /// <summary>
        /// Gets the optional path to the suppression file.
        /// </summary>
        public string SuppressionFilePath { get; }

        /// <summary>
        /// Gets the optional path to the folder for reports.
        /// </summary>
        public string ReportFolderPath { get; }

        /// <summary>
        /// Gets a value indicating whether machine-readable JSON output was requested.
        /// </summary>
        public bool Json { get; }

        /// <summary>
        /// Gets the severity at or above which findings cause a non-zero <c>lint</c> exit code
        /// (<see cref="MessageSeverity.Error"/> is the most severe). Defaults to
        /// <see cref="MessageSeverity.Error"/>, so warnings and info do not gate a build unless
        /// <c>--max-severity</c> lowers the bar.
        /// </summary>
        public MessageSeverity MaxSeverity { get; }

        /// <summary>
        /// Gets the variables defined on the command line.
        /// </summary>
        public IDictionary<string, string> Variables { get; } = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Tries to parse the arguments into an instance of the <see cref="LintArguments"/> class.
        /// </summary>
        /// <param name="args">The raw program arguments.</param>
        /// <param name="result">on success, receives an instance to a new instance of the <see cref="LintArguments"/> class.</param>
        /// <returns><c>True</c> if the arguments were parsed succesfully; otherwise, <c>false</c>.</returns>
        public static bool TryParse(string[] args, out LintArguments? result)
        {
            result = null;
            if (args.Length < 1)
            {
                return false;
            }

            CliArgs parsed = CliArgs.Parse(args, ValueOptionNames);
            if (parsed.Help || parsed.UnknownFlags.Count > 0 || parsed.Positionals.Count != 1)
            {
                return false;
            }

            string path = parsed.Positionals[0];
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            Dictionary<string, string> variables = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (string define in parsed.Defines)
            {
                string[] parts = define.Split('=', StringSplitOptions.None);
                if (parts.Length != 2)
                {
                    return false;
                }

                string name = parts[0];
                string value = parts[1];
                if (name.Length == 0 || !name.All(e => char.IsLetterOrDigit(e) || e == '_'))
                {
                    return false;
                }

                variables[name] = value;
            }

            MessageSeverity maxSeverity = MessageSeverity.Error;
            string? maxSeverityText = parsed.Get("max-severity");
            if (!string.IsNullOrEmpty(maxSeverityText))
            {
                switch (maxSeverityText!.ToLowerInvariant())
                {
                    case "error":
                        maxSeverity = MessageSeverity.Error;
                        break;
                    case "warning":
                        maxSeverity = MessageSeverity.Warning;
                        break;
                    case "info":
                    case "information":
                        maxSeverity = MessageSeverity.Info;
                        break;
                    default:
                        return false;
                }
            }

            result = new LintArguments(
                path,
                parsed.Get("ruleset") ?? string.Empty,
                parsed.Get("suppressionFile") ?? string.Empty,
                parsed.Get("reportFolder") ?? string.Empty,
                variables,
                parsed.Json,
                maxSeverity);
            return true;
        }
    }
}
