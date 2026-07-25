namespace ThreatModelForge.Engine
{
    /// <summary>
    /// A declarative rule pack supplied to the engine as content. Hosts that cannot resolve filesystem
    /// paths (the HTTP API and the in-browser engine) send the pack JSON here; hosts that can (the CLI
    /// and the MCP server) resolve and read the file themselves, so path handling never leaks into the
    /// engine facade.
    /// </summary>
    public sealed class RuleSourceDto
    {
        /// <summary>
        /// Gets the logical origin reported in diagnostics (for example, <c>corporate.tmrules.json</c>).
        /// It is never resolved against the filesystem.
        /// </summary>
        public string? Name { get; init; }

        /// <summary>Gets the rule pack JSON.</summary>
        public string? Json { get; init; }
    }
}
