namespace ThreatModelForge.Editing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;

    /// <summary>
    /// The built-in typed property schema served to authoring surfaces. The schema is data-driven:
    /// the embedded <c>property-schema.json</c> maps each DFD primitive to its known custom
    /// properties, each property's value kind (enum/bool/string), allowed values, and default.
    /// Studio uses it to render typed controls and emit canonical values; the analysis rules then
    /// read those values with confidence. Loaded once from this assembly's manifest at startup.
    /// </summary>
    public static class PropertySchemaCatalog
    {
        /// <summary>Shared, case-insensitive options for reading the schema file.</summary>
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        /// <summary>The flattened schema, loaded once at startup.</summary>
        private static readonly IReadOnlyList<PropertyDescriptor> Descriptors = Load();

        /// <summary>Gets every property descriptor across all primitives, in file order.</summary>
        public static IReadOnlyList<PropertyDescriptor> All => Descriptors;

        /// <summary>Gets the property descriptors that apply to a DFD primitive (case-insensitive).</summary>
        /// <param name="appliesTo">The DFD primitive (<c>process</c>, <c>datastore</c>, <c>external</c>, or <c>flow</c>).</param>
        /// <returns>The matching descriptors, or an empty list when none apply.</returns>
        public static IReadOnlyList<PropertyDescriptor> For(string appliesTo)
        {
            return Descriptors
                .Where(descriptor => string.Equals(descriptor.AppliesTo, appliesTo, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Validates a single custom-property assignment against the typed schema for a DFD primitive.
        /// Enum and bool values are matched case-insensitively; when matched, <paramref name="canonicalValue"/>
        /// is set to the schema's canonical casing so the analysis rules match reliably. Free-form
        /// string properties (no allowed values) accept any value.
        /// </summary>
        /// <param name="appliesTo">The DFD primitive (<c>process</c>, <c>datastore</c>, <c>external</c>, or <c>flow</c>).</param>
        /// <param name="name">The property name from the assignment.</param>
        /// <param name="value">The value from the assignment.</param>
        /// <param name="canonicalValue">On return, the canonical value to store (schema casing when the value matched).</param>
        /// <returns>An issue when the property or value is not in the schema; otherwise <see langword="null"/>.</returns>
        public static PropertySchemaIssue? Validate(string appliesTo, string name, string value, out string canonicalValue)
        {
            canonicalValue = value;
            IReadOnlyList<PropertyDescriptor> descriptors = For(appliesTo);
            PropertyDescriptor? descriptor = descriptors
                .FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));

            if (descriptor is null)
            {
                List<string> known = new List<string>();
                foreach (PropertyDescriptor candidate in descriptors)
                {
                    known.Add(candidate.Name);
                }

                return new PropertySchemaIssue
                {
                    AppliesTo = appliesTo,
                    Kind = PropertySchemaIssueKind.UnknownProperty,
                    Property = name,
                    Value = value,
                    Allowed = known,
                };
            }

            if (descriptor.Values.Count == 0)
            {
                return null;
            }

            foreach (string allowed in descriptor.Values.Where(allowed => string.Equals(allowed, value, StringComparison.OrdinalIgnoreCase)))
            {
                canonicalValue = allowed;
                return null;
            }

            return new PropertySchemaIssue
            {
                AppliesTo = appliesTo,
                Kind = PropertySchemaIssueKind.InvalidValue,
                Property = descriptor.Name,
                Value = value,
                Allowed = descriptor.Values,
            };
        }

        /// <summary>Reads and flattens the embedded property schema, or empty when it is absent.</summary>
        /// <returns>The flattened descriptors, each tagged with the primitive it applies to.</returns>
        private static IReadOnlyList<PropertyDescriptor> Load()
        {
            Assembly assembly = typeof(PropertySchemaCatalog).Assembly;
            string? resource = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith("property-schema.json", StringComparison.OrdinalIgnoreCase));
            if (resource is null)
            {
                return Array.Empty<PropertyDescriptor>();
            }

            using Stream stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Property-schema resource '{resource}' could not be opened.");
            using StreamReader reader = new StreamReader(stream);
            Dictionary<string, List<SchemaEntry>>? byPrimitive =
                JsonSerializer.Deserialize<Dictionary<string, List<SchemaEntry>>>(reader.ReadToEnd(), SerializerOptions);
            if (byPrimitive is null)
            {
                return Array.Empty<PropertyDescriptor>();
            }

            List<PropertyDescriptor> descriptors = new List<PropertyDescriptor>();
            foreach (KeyValuePair<string, List<SchemaEntry>> primitive in byPrimitive)
            {
                foreach (SchemaEntry entry in primitive.Value)
                {
                    descriptors.Add(new PropertyDescriptor
                    {
                        AppliesTo = primitive.Key,
                        Name = entry.Name,
                        Kind = entry.Kind,
                        Values = entry.Values ?? new List<string>(),
                        Default = entry.Default,
                    });
                }
            }

            return descriptors;
        }

        /// <summary>The on-disk shape of a single property descriptor within the schema file.</summary>
        private sealed class SchemaEntry
        {
            /// <summary>Gets or sets the property name.</summary>
            public string Name { get; set; } = string.Empty;

            /// <summary>Gets or sets the value kind (<c>enum</c>, <c>bool</c>, or <c>string</c>).</summary>
            public string Kind { get; set; } = string.Empty;

            /// <summary>Gets or sets the allowed values.</summary>
            public List<string>? Values { get; set; }

            /// <summary>Gets or sets the default value.</summary>
            public string Default { get; set; } = string.Empty;
        }
    }
}
