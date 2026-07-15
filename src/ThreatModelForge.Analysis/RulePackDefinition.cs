namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The identity, source metadata, and catalogs carried by a version 2 rule pack. Instances
    /// returned by <see cref="DeclarativeRuleProvider.LoadBundle"/> have passed the loader's
    /// validation and expose defensive collection copies.
    /// </summary>
    public sealed class RulePackDefinition
    {
        private readonly Dictionary<string, RuleElementTypeDefinition> elementTypesById;
        private readonly Dictionary<string, RulePropertyDefinition> propertiesByName;
        private readonly Dictionary<string, RuleCategoryDefinition> categoriesById;

        /// <summary>Initializes a new instance of the <see cref="RulePackDefinition"/> class.</summary>
        /// <param name="id">The stable pack identifier.</param>
        /// <param name="dialect">The rule language dialect.</param>
        /// <param name="name">The display name.</param>
        /// <param name="description">The optional description.</param>
        /// <param name="version">The optional source version.</param>
        /// <param name="fingerprint">The computed content fingerprint.</param>
        /// <param name="source">The source metadata.</param>
        /// <param name="categories">The category catalog.</param>
        /// <param name="elementTypes">The element-type catalog.</param>
        /// <param name="properties">The property catalog.</param>
        internal RulePackDefinition(
            string id,
            string dialect,
            string name,
            string? description,
            string? version,
            string fingerprint,
            RulePackSource? source,
            IReadOnlyList<RuleCategoryDefinition> categories,
            IReadOnlyList<RuleElementTypeDefinition> elementTypes,
            IReadOnlyList<RulePropertyDefinition> properties)
        {
            this.Id = id;
            this.Dialect = dialect;
            this.Name = name;
            this.Description = description;
            this.Version = version;
            this.Fingerprint = fingerprint;
            this.Source = source;
            this.Categories = categories;
            this.ElementTypes = elementTypes;
            this.Properties = properties;
            this.categoriesById = new Dictionary<string, RuleCategoryDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (RuleCategoryDefinition category in categories)
            {
                this.categoriesById[category.Id] = category;
            }

            this.elementTypesById = new Dictionary<string, RuleElementTypeDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (RuleElementTypeDefinition elementType in elementTypes)
            {
                this.elementTypesById[elementType.Id] = elementType;
            }

            this.propertiesByName = new Dictionary<string, RulePropertyDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (RulePropertyDefinition property in properties)
            {
                this.propertiesByName[property.Name] = property;
                foreach (string alias in property.Aliases)
                {
                    this.propertiesByName[alias] = property;
                }
            }
        }

        /// <summary>Gets the stable pack identifier.</summary>
        public string Id { get; }

        /// <summary>Gets the namespaced rule language dialect used by this pack.</summary>
        public string Dialect { get; }

        /// <summary>Gets the pack display name.</summary>
        public string Name { get; }

        /// <summary>Gets the optional pack description.</summary>
        public string? Description { get; }

        /// <summary>Gets the optional pack version.</summary>
        public string? Version { get; }

        /// <summary>Gets the optional content fingerprint.</summary>
        public string Fingerprint { get; }

        /// <summary>Gets metadata about the source artifact.</summary>
        public RulePackSource? Source { get; }

        /// <summary>Gets the threat category catalog.</summary>
        public IReadOnlyList<RuleCategoryDefinition> Categories { get; }

        /// <summary>Gets the element-type hierarchy catalog.</summary>
        public IReadOnlyList<RuleElementTypeDefinition> ElementTypes { get; }

        /// <summary>Gets the source property and alias catalog.</summary>
        public IReadOnlyList<RulePropertyDefinition> Properties { get; }

        /// <summary>Resolves a source category identifier case-insensitively.</summary>
        /// <param name="id">The source category identifier.</param>
        /// <returns>The canonical category, or <see langword="null"/>.</returns>
        internal RuleCategoryDefinition? ResolveCategory(string id)
        {
            return this.categoriesById.TryGetValue(id, out RuleCategoryDefinition? category)
                ? category
                : null;
        }

        /// <summary>Resolves an element type identifier case-insensitively.</summary>
        /// <param name="id">The element type identifier.</param>
        /// <returns>The canonical element type, or <see langword="null"/>.</returns>
        internal RuleElementTypeDefinition? ResolveElementType(string id)
        {
            return this.elementTypesById.TryGetValue(id, out RuleElementTypeDefinition? elementType)
                ? elementType
                : null;
        }

        /// <summary>Resolves a canonical property name or alias case-insensitively.</summary>
        /// <param name="name">The property name or alias.</param>
        /// <returns>The canonical property definition, or <see langword="null"/>.</returns>
        internal RulePropertyDefinition? ResolveProperty(string name)
        {
            return this.propertiesByName.TryGetValue(name, out RulePropertyDefinition? property)
                ? property
                : null;
        }
    }
}
