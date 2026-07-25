namespace ThreatModelForge.Engine
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>
    /// An optional mapping from tmforge rules to an engagement's own threat taxonomy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Teams that already run a threat catalogue need their ids on the evidence, but that catalogue
    /// must not become part of detection. So this is applied strictly after projection: it annotates
    /// findings that are already decided, and no rule fires, changes severity, or changes disposition
    /// because of it. Removing the mapping changes the annotations and nothing else.
    /// </para>
    /// <para>
    /// A rule with no entry gets no canonical ids. Nothing is inferred from the rule id, the STRIDE
    /// category, or anything else — an invented mapping is worse than an absent one, because a reader
    /// cannot tell it was guessed.
    /// </para>
    /// </remarks>
    public sealed class AnalysisTaxonomyMap
    {
        /// <summary>The schema tag a taxonomy mapping file carries.</summary>
        public const string SchemaName = "tmforge-taxonomy";

        /// <summary>The current mapping schema version.</summary>
        public const int CurrentVersion = 1;

        private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>Gets the schema tag.</summary>
        public string Schema { get; init; } = SchemaName;

        /// <summary>Gets the mapping schema version.</summary>
        public int Version { get; init; } = CurrentVersion;

        /// <summary>
        /// Gets the canonical ids for each rule, keyed by the rule's effective id (pack-qualified for a
        /// custom rule). A rule that is absent here is deliberately unmapped.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>>? Rules { get; init; }

        /// <summary>Parses a mapping file.</summary>
        /// <param name="json">The mapping document.</param>
        /// <param name="map">On success, the parsed mapping.</param>
        /// <param name="problems">The reasons the mapping was refused, empty on success.</param>
        /// <returns><see langword="true"/> when the mapping was read.</returns>
        public static bool TryRead(string json, out AnalysisTaxonomyMap? map, out IReadOnlyList<string> problems)
        {
            map = null;
            List<string> found = new List<string>();
            problems = found;

            AnalysisTaxonomyMap? parsed;
            try
            {
                // Checked against the raw JSON first: this type defaults its schema and version, so a
                // file declaring neither would otherwise look like a valid mapping and silently
                // contribute nothing.
                using (JsonDocument probe = JsonDocument.Parse(json))
                {
                    if (probe.RootElement.ValueKind != JsonValueKind.Object ||
                        !probe.RootElement.TryGetProperty("schema", out JsonElement schema) ||
                        schema.ValueKind != JsonValueKind.String)
                    {
                        found.Add($"This is not a {SchemaName} document: it declares no schema.");
                        return false;
                    }

                    if (!string.Equals(schema.GetString(), SchemaName, StringComparison.Ordinal))
                    {
                        found.Add($"This is not a {SchemaName} document (its schema is '{schema.GetString()}').");
                        return false;
                    }
                }

                parsed = JsonSerializer.Deserialize<AnalysisTaxonomyMap>(json, ReadOptions);
            }
            catch (JsonException ex)
            {
                found.Add("The taxonomy mapping is not valid JSON: " + ex.Message);
                return false;
            }

            if (parsed == null)
            {
                found.Add("The taxonomy mapping is empty.");
                return false;
            }

            if (parsed.Version != CurrentVersion)
            {
                found.Add($"Unsupported taxonomy mapping version {parsed.Version}; this build reads version {CurrentVersion}.");
                return false;
            }

            map = parsed;
            return true;
        }

        /// <summary>
        /// Returns the canonical ids recorded for a rule, or an empty list when the rule is unmapped.
        /// </summary>
        /// <param name="ruleId">The rule's effective id.</param>
        /// <returns>The canonical ids, in the order the mapping declares them.</returns>
        public IReadOnlyList<string> For(string? ruleId)
        {
            if (string.IsNullOrEmpty(ruleId) ||
                this.Rules == null ||
                !this.Rules.TryGetValue(ruleId!, out IReadOnlyList<string>? ids) ||
                ids == null)
            {
                return Array.Empty<string>();
            }

            List<string> canonical = new List<string>();
            foreach (string id in ids)
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    canonical.Add(id.Trim());
                }
            }

            return canonical;
        }
    }
}
