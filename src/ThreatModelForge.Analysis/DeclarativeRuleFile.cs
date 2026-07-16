namespace ThreatModelForge.Analysis
{
    using System.Collections.Generic;

    /// <summary>
    /// The root of a declarative rule spec file (<c>*.tmrules.json</c>): a list of rule definitions
    /// authored as data rather than compiled code. Because a spec is inspectable data — not arbitrary
    /// code — it is the safe way to ship shared or third-party rule packs. Deserialized by
    /// <see cref="DeclarativeRuleProvider"/>.
    /// </summary>
    internal sealed class DeclarativeRuleFile
    {
        /// <summary>Gets or sets the schema discriminator for a versioned pack.</summary>
        public string? Schema { get; set; }

        /// <summary>Gets or sets the schema version; absent for legacy rule files.</summary>
        public int? Version { get; set; }

        /// <summary>Gets or sets the namespaced rule language dialect.</summary>
        public string? Dialect { get; set; }

        /// <summary>Gets or sets the versioned pack identity and source metadata.</summary>
        public PackSpec? Pack { get; set; }

        /// <summary>Gets or sets the versioned threat category catalog.</summary>
        public List<CategorySpec>? Categories { get; set; }

        /// <summary>Gets or sets the versioned element-type hierarchy catalog.</summary>
        public List<ElementTypeSpec>? ElementTypes { get; set; }

        /// <summary>Gets or sets the versioned source property catalog.</summary>
        public List<PropertySpec>? Properties { get; set; }

        /// <summary>Gets or sets the rules declared in the file.</summary>
        public List<DeclarativeRuleSpec>? Rules { get; set; }

        /// <summary>The mutable JSON shape of a pack header.</summary>
        internal sealed class PackSpec
        {
            /// <summary>Gets or sets the pack id.</summary>
            public string? Id { get; set; }

            /// <summary>Gets or sets the pack display name.</summary>
            public string? Name { get; set; }

            /// <summary>Gets or sets the optional description.</summary>
            public string? Description { get; set; }

            /// <summary>Gets or sets the optional source version.</summary>
            public string? Version { get; set; }

            /// <summary>Gets or sets a caller-supplied fingerprint, which validation rejects.</summary>
            public string? Fingerprint { get; set; }

            /// <summary>Gets or sets source metadata.</summary>
            public SourceSpec? Source { get; set; }
        }

        /// <summary>The mutable JSON shape of source metadata.</summary>
        internal sealed class SourceSpec
        {
            /// <summary>Gets or sets the namespaced source type.</summary>
            public string? Type { get; set; }

            /// <summary>Gets or sets the optional source name.</summary>
            public string? Name { get; set; }

            /// <summary>Gets or sets the optional source id.</summary>
            public string? Id { get; set; }

            /// <summary>Gets or sets the optional source version.</summary>
            public string? Version { get; set; }

            /// <summary>Gets or sets the optional source URI.</summary>
            public string? Uri { get; set; }

            /// <summary>Gets or sets the optional source fingerprint.</summary>
            public string? Fingerprint { get; set; }

            /// <summary>Gets or sets importer-neutral source metadata not otherwise represented.</summary>
            public List<SourceMetadataSpec>? Metadata { get; set; }
        }

        /// <summary>One preserved template-level source metadata value.</summary>
        internal sealed class SourceMetadataSpec
        {
            /// <summary>Gets or sets the namespaced metadata key.</summary>
            public string? Key { get; set; }

            /// <summary>Gets or sets the preserved metadata value.</summary>
            public string? Value { get; set; }
        }

        /// <summary>The mutable JSON shape of a category definition.</summary>
        internal sealed class CategorySpec
        {
            /// <summary>Gets or sets the category id.</summary>
            public string? Id { get; set; }

            /// <summary>Gets or sets the display name.</summary>
            public string? Name { get; set; }

            /// <summary>Gets or sets the optional short description.</summary>
            public string? ShortDescription { get; set; }

            /// <summary>Gets or sets the optional long description.</summary>
            public string? LongDescription { get; set; }
        }

        /// <summary>The mutable JSON shape of an element-type definition.</summary>
        internal sealed class ElementTypeSpec
        {
            /// <summary>Gets or sets the element-type id.</summary>
            public string? Id { get; set; }

            /// <summary>Gets or sets the display name.</summary>
            public string? Name { get; set; }

            /// <summary>Gets or sets the optional parent id.</summary>
            public string? ParentId { get; set; }
        }

        /// <summary>The mutable JSON shape of a property definition.</summary>
        internal sealed class PropertySpec
        {
            /// <summary>Gets or sets the canonical runtime property name.</summary>
            public string? Name { get; set; }

            /// <summary>Gets or sets source aliases.</summary>
            public List<string>? Aliases { get; set; }

            /// <summary>Gets or sets allowed values.</summary>
            public List<string>? AllowedValues { get; set; }

            /// <summary>Gets or sets applicable element-type ids.</summary>
            public List<string>? ElementTypeIds { get; set; }
        }
    }
}
