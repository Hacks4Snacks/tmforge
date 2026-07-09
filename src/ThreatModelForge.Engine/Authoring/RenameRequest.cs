namespace ThreatModelForge.Engine
{
    /// <summary>
    /// The inputs for <see cref="AuthoringOperations.Rename"/>: the element reference, the new name,
    /// and an optional page scope.
    /// </summary>
    public sealed class RenameRequest
    {
        /// <summary>Gets the element reference (GUID, alias, or unique name).</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the new display name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets the page to scope the search to (a 1-based index or a page name); when omitted every page is searched.</summary>
        public string? Page { get; init; }
    }
}
