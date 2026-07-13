namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ThreatModelForge.KnowledgeBase;

    /// <summary>
    /// Resolves MTMT element-type and property aliases to canonical runtime names.
    /// </summary>
    internal sealed class MtmtGenerationFilterCatalog
    {
        private readonly Dictionary<string, string> properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> ambiguousProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> staticPropertyValues =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> dynamicProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> types;

        /// <summary>Initializes a new instance of the <see cref="MtmtGenerationFilterCatalog"/> class.</summary>
        /// <param name="knowledgeBase">The source MTMT knowledge base.</param>
        internal MtmtGenerationFilterCatalog(KnowledgeBaseData knowledgeBase)
        {
            _ = knowledgeBase ?? throw new ArgumentNullException(nameof(knowledgeBase));
            List<ElementType> elements = knowledgeBase.GenericElements.Concat(knowledgeBase.StandardElements).ToList();
            this.types = elements
                .Where(element => !string.IsNullOrWhiteSpace(element.Id))
                .ToDictionary(element => element.Id!, element => element.Id!, StringComparer.OrdinalIgnoreCase);

            foreach (KnowledgeBaseAttribute attribute in elements.SelectMany(element => element.Attributes))
            {
                string? canonical = FirstNonEmpty(attribute.DisplayName, attribute.Name, attribute.Id);
                if (canonical == null)
                {
                    continue;
                }

                this.AddPropertyAlias(attribute.DisplayName, canonical);
                this.AddPropertyAlias(attribute.Name, canonical);
                this.AddPropertyAlias(attribute.Id, canonical);
                this.AddPropertyValues(canonical, attribute);
            }
        }

        /// <summary>Resolves an element-type identifier.</summary>
        /// <param name="value">The source identifier.</param>
        /// <returns>The canonical identifier.</returns>
        internal string ResolveType(string value)
        {
            if (string.Equals(value, "ROOT", StringComparison.OrdinalIgnoreCase))
            {
                return "ROOT";
            }

            if (this.types.TryGetValue(value, out string? canonical))
            {
                return canonical;
            }

            throw new FormatException($"GenerationFilters references unknown element type '{value}'.");
        }

        /// <summary>Resolves a property identifier or display alias.</summary>
        /// <param name="value">The source property token.</param>
        /// <returns>The canonical runtime property name.</returns>
        internal string ResolveProperty(string value)
        {
            if (this.ambiguousProperties.Contains(value))
            {
                throw new FormatException($"GenerationFilters property '{value}' is ambiguous.");
            }

            if (this.properties.TryGetValue(value, out string? canonical))
            {
                return canonical;
            }

            throw new FormatException($"GenerationFilters references unknown property '{value}'.");
        }

        /// <summary>Resolves a property and validates a static source value.</summary>
        /// <param name="property">The source property token.</param>
        /// <param name="value">The source property value.</param>
        /// <returns>The canonical runtime property name.</returns>
        internal string ResolvePropertyValue(string property, string value)
        {
            string canonical = this.ResolveProperty(property);
            if (!this.dynamicProperties.Contains(canonical) &&
                this.staticPropertyValues.TryGetValue(canonical, out HashSet<string>? allowed) &&
                !allowed.Contains(value))
            {
                throw new FormatException(
                    $"GenerationFilters property '{property}' does not allow value '{value}'.");
            }

            return canonical;
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        private void AddPropertyAlias(string? alias, string canonical)
        {
            if (string.IsNullOrWhiteSpace(alias) || this.ambiguousProperties.Contains(alias!))
            {
                return;
            }

            if (this.properties.TryGetValue(alias!, out string? existing) &&
                !string.Equals(existing, canonical, StringComparison.OrdinalIgnoreCase))
            {
                this.properties.Remove(alias!);
                this.ambiguousProperties.Add(alias!);
                return;
            }

            this.properties[alias!] = canonical;
        }

        private void AddPropertyValues(string canonical, KnowledgeBaseAttribute attribute)
        {
            if (attribute.Mode == AttributeMode.Dynamic)
            {
                this.dynamicProperties.Add(canonical);
                this.staticPropertyValues.Remove(canonical);
                return;
            }

            if (this.dynamicProperties.Contains(canonical))
            {
                return;
            }

            if (!this.staticPropertyValues.TryGetValue(canonical, out HashSet<string>? allowed))
            {
                allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                this.staticPropertyValues[canonical] = allowed;
            }

            allowed.UnionWith(attribute.AttributeValues);
        }
    }
}
