namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Options that select which rule sources contribute to a loaded <see cref="RuleSet"/> beyond the
    /// built-in first-party rules. Custom rules are strictly opt-in: an empty set of options yields only
    /// the built-in rules.
    /// </summary>
    public sealed class RuleSourceOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RuleSourceOptions"/> class.
        /// </summary>
        /// <param name="specPaths">The declarative rule spec files or directories to load, or <see langword="null"/> for none.</param>
        /// <param name="diagnostics">An optional sink for non-fatal load and validation warnings.</param>
        /// <param name="contents">The in-memory rule documents to load, or <see langword="null"/> for none.</param>
        public RuleSourceOptions(
            IEnumerable<string>? specPaths = null,
            Action<string>? diagnostics = null,
            IEnumerable<RuleContent>? contents = null)
        {
            this.SpecPaths = specPaths?.Where(path => !string.IsNullOrWhiteSpace(path)).ToList() ?? new List<string>();
            this.Contents = contents?.Where(content => content != null).ToList() ?? new List<RuleContent>();
            this.Diagnostics = diagnostics;
        }

        /// <summary>Gets the declarative rule spec files or directories to load.</summary>
        public IReadOnlyList<string> SpecPaths { get; }

        /// <summary>
        /// Gets the in-memory rule documents to load. Hosts that cannot resolve filesystem paths — the
        /// HTTP API and the in-browser engine — supply rules here instead of through
        /// <see cref="SpecPaths"/>.
        /// </summary>
        public IReadOnlyList<RuleContent> Contents { get; }

        /// <summary>Gets the optional sink for non-fatal load and validation warnings.</summary>
        public Action<string>? Diagnostics { get; }

        /// <summary>Gets a value indicating whether any custom rule source is selected.</summary>
        public bool IsEmpty => this.SpecPaths.Count == 0 && this.Contents.Count == 0;
    }
}
