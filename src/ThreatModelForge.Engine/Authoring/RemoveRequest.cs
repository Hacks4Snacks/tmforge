namespace ThreatModelForge.Engine
{
    /// <summary>
    /// The inputs for <see cref="AuthoringOperations.Remove"/>: the element reference and an optional
    /// page scope. Removing a component also removes any data flows attached to it.
    /// </summary>
    public sealed class RemoveRequest
    {
        /// <summary>Gets the element reference (GUID, alias, or unique name).</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the page to scope the search to (a 1-based index or a page name); when omitted every page is searched.</summary>
        public string? Page { get; init; }
    }
}
