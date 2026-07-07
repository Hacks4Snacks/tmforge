namespace ThreatModelForge.Editing
{
    /// <summary>
    /// A single attribute-level change to an element between two models: the attribute key and its
    /// value on each side. <see cref="From"/> is <see langword="null"/> when the attribute was absent
    /// on the base model; <see cref="To"/> is <see langword="null"/> when it was absent on the revised
    /// model.
    /// </summary>
    public sealed class PropertyChange
    {
        /// <summary>Gets the attribute key (for example, <c>name</c>, <c>Protocol</c>, or <c>target</c>).</summary>
        public string Key { get; init; } = string.Empty;

        /// <summary>Gets the value on the base model, or <see langword="null"/> when the attribute was absent.</summary>
        public string? From { get; init; }

        /// <summary>Gets the value on the revised model, or <see langword="null"/> when the attribute was absent.</summary>
        public string? To { get; init; }
    }
}
