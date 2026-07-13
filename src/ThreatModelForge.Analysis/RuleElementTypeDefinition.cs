namespace ThreatModelForge.Analysis
{
    /// <summary>
    /// Defines an element type and its parent link for hierarchy-aware rule evaluation.
    /// </summary>
    public sealed class RuleElementTypeDefinition
    {
        /// <summary>Initializes a new instance of the <see cref="RuleElementTypeDefinition"/> class.</summary>
        /// <param name="id">The element-type identifier.</param>
        /// <param name="name">The display name.</param>
        /// <param name="parentId">The optional parent identifier.</param>
        internal RuleElementTypeDefinition(string id, string name, string? parentId)
        {
            this.Id = id;
            this.Name = name;
            this.ParentId = parentId;
        }

        /// <summary>Gets the element-type identifier.</summary>
        public string Id { get; }

        /// <summary>Gets the element-type display name.</summary>
        public string Name { get; }

        /// <summary>Gets the parent element-type identifier, or <c>ROOT</c>.</summary>
        public string? ParentId { get; }
    }
}
