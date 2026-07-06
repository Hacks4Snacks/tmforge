namespace ThreatModelForge.Editing
{
    /// <summary>
    /// Describes a stencil pack: a named, togglable group of related stencils (for example, the
    /// Azure pack). The palette uses packs so the user can show or hide whole families at once.
    /// </summary>
    public sealed class PackDto
    {
        /// <summary>Gets the stable pack identifier (for example, <c>azure</c>).</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets the human-readable pack name shown in the palette.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Gets the number of stencils the pack contributes.</summary>
        public int Count { get; init; }
    }
}
