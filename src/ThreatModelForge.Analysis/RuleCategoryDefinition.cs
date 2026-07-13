namespace ThreatModelForge.Analysis
{
    /// <summary>
    /// Defines a threat category carried by a versioned rule pack.
    /// </summary>
    public sealed class RuleCategoryDefinition
    {
        /// <summary>Initializes a new instance of the <see cref="RuleCategoryDefinition"/> class.</summary>
        /// <param name="id">The category identifier.</param>
        /// <param name="name">The display name.</param>
        /// <param name="shortDescription">The optional short description.</param>
        /// <param name="longDescription">The optional long description.</param>
        internal RuleCategoryDefinition(string id, string name, string? shortDescription, string? longDescription)
        {
            this.Id = id;
            this.Name = name;
            this.ShortDescription = shortDescription;
            this.LongDescription = longDescription;
        }

        /// <summary>Gets the category identifier used by source rules.</summary>
        public string Id { get; }

        /// <summary>Gets the category display name.</summary>
        public string Name { get; }

        /// <summary>Gets the optional short description.</summary>
        public string? ShortDescription { get; }

        /// <summary>Gets the optional long description.</summary>
        public string? LongDescription { get; }
    }
}
