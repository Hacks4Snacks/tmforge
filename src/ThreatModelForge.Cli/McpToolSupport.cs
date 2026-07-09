namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Shared helpers for the <c>tmforge mcp</c> tool classes: property-assignment marshaling and the
    /// grounding text an agent needs to author declarative manifests.
    /// </summary>
    internal static class McpToolSupport
    {
        /// <summary>
        /// A human-readable description of the declarative manifest shape accepted by the
        /// <c>apply</c> tool, returned by the <c>manifest_schema</c> tool as agent grounding.
        /// </summary>
        public const string ManifestSchemaText =
            "A declarative threat-model manifest is a JSON object with these fields:\n" +
            "{\n" +
            "  \"name\": \"<model title>\",\n" +
            "  \"boundaries\": [ { \"alias\": \"<stable id>\", \"name\": \"<label>\" } ],\n" +
            "  \"elements\": [ {\n" +
            "    \"alias\": \"<stable id>\",\n" +
            "    \"kind\": \"process | store | external\",   // omit when 'stencil' is set\n" +
            "    \"name\": \"<label>\",\n" +
            "    \"stencil\": \"<stencil id from the stencils tool>\",  // optional\n" +
            "    \"boundary\": \"<boundary alias>\",           // optional\n" +
            "    \"props\": { \"<Key>\": \"<Value>\" }            // optional, validated against property_schema\n" +
            "  } ],\n" +
            "  \"flows\": [ {\n" +
            "    \"from\": \"<element alias or unique name>\",\n" +
            "    \"to\": \"<element alias or unique name>\",\n" +
            "    \"name\": \"<label>\",\n" +
            "    \"props\": { \"Protocol\": \"HTTPS\", \"Port\": \"443\" }   // optional\n" +
            "  } ]\n" +
            "}\n" +
            "Elements and flow endpoints are referenced by alias (or unique name), so the manifest needs no GUIDs " +
            "and round-trips with the export_manifest tool. Property values are validated against the property_schema " +
            "tool's catalog; pass force=true to store unknown names or values.";

        /// <summary>
        /// Marshals a property map (a JSON object of key/value strings) into the <c>KEY=VALUE</c>
        /// assignment list the authoring requests consume.
        /// </summary>
        /// <param name="properties">The property map, or <see langword="null"/>.</param>
        /// <returns>The assignments, or an empty list.</returns>
        public static IReadOnlyList<string> ToAssignments(IReadOnlyDictionary<string, string>? properties)
        {
            if (properties == null || properties.Count == 0)
            {
                return Array.Empty<string>();
            }

            List<string> assignments = new List<string>(properties.Count);
            foreach (KeyValuePair<string, string> pair in properties)
            {
                assignments.Add(pair.Key + "=" + pair.Value);
            }

            return assignments;
        }
    }
}
