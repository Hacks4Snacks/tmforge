namespace ThreatModelForge.Engine
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.Json;

    /// <summary>
    /// Reads a stored <c>tmforge-analysis</c> document, refusing anything this build cannot honestly
    /// interpret.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A stored analysis outlives the tool that wrote it, so the version has to be checked rather than
    /// assumed. Reading a future document with today's rules would silently misinterpret fields that
    /// have changed meaning, which is worse than refusing: the caller would act on evidence it does not
    /// actually understand.
    /// </para>
    /// <para>
    /// Version 1 is currently the only version, so there is nothing to migrate from yet. When a version
    /// 2 exists, the upgrade belongs here — between parsing and validation — so every consumer inherits
    /// it, and an older document is migrated rather than rejected.
    /// </para>
    /// </remarks>
    public static class AnalysisDocumentReader
    {
        private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>Reads a document and checks that this build can interpret it.</summary>
        /// <param name="json">The stored document.</param>
        /// <param name="expectedVersion">
        /// The schema version the caller was written against. Supply this to pin the contract: a
        /// document of any other version is refused instead of being read on different assumptions.
        /// </param>
        /// <param name="document">On success, the parsed document.</param>
        /// <param name="problems">The reasons the document was refused, empty on success.</param>
        /// <returns><see langword="true"/> when the document was read.</returns>
        public static bool TryRead(
            string json,
            int? expectedVersion,
            out AnalysisDocumentDto? document,
            out IReadOnlyList<string> problems)
        {
            document = null;
            List<string> found = new List<string>();
            problems = found;

            try
            {
                // The envelope is checked against the raw JSON, not against the deserialized object.
                // The DTO supplies defaults for schema and version, so a file that declares neither
                // would otherwise deserialize into something that looks like a valid version 1
                // document — which is precisely the silent misread this class exists to prevent.
                using (JsonDocument probe = JsonDocument.Parse(json))
                {
                    if (!CheckEnvelope(probe.RootElement, expectedVersion, found))
                    {
                        return false;
                    }
                }

                document = JsonSerializer.Deserialize<AnalysisDocumentDto>(json, ReadOptions);
            }
            catch (JsonException ex)
            {
                found.Add("The file is not valid JSON: " + ex.Message);
                return false;
            }

            if (document == null)
            {
                found.Add("The file is empty.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks that the file declares itself to be a document this build can read.
        /// </summary>
        /// <param name="root">The parsed root element.</param>
        /// <param name="expectedVersion">The version the caller pinned, if any.</param>
        /// <param name="problems">Receives the reasons the file was refused.</param>
        /// <returns><see langword="true"/> when the envelope is acceptable.</returns>
        private static bool CheckEnvelope(JsonElement root, int? expectedVersion, ICollection<string> problems)
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                problems.Add("The file is not a JSON object.");
                return false;
            }

            if (!TryGet(root, "schema", out JsonElement schema) || schema.ValueKind != JsonValueKind.String)
            {
                problems.Add(
                    $"This is not a {AnalysisDocumentDto.SchemaName} document: it declares no schema. " +
                    "The findings JSON and the listing written beside it are different artifacts.");
                return false;
            }

            if (!string.Equals(schema.GetString(), AnalysisDocumentDto.SchemaName, StringComparison.Ordinal))
            {
                problems.Add(
                    $"This is not a {AnalysisDocumentDto.SchemaName} document (its schema is " +
                    $"'{schema.GetString()}').");
                return false;
            }

            if (!TryGet(root, "version", out JsonElement version) ||
                version.ValueKind != JsonValueKind.Number ||
                !version.TryGetInt32(out int declared))
            {
                problems.Add("The document declares no schema version, so it cannot be read safely.");
                return false;
            }

            return CheckVersion(declared, expectedVersion, problems);
        }

        private static bool TryGet(JsonElement root, string name, out JsonElement value)
        {
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static bool CheckVersion(int version, int? expectedVersion, ICollection<string> problems)
        {
            string current = AnalysisDocumentDto.CurrentVersion.ToString(CultureInfo.InvariantCulture);
            string actual = version.ToString(CultureInfo.InvariantCulture);

            if (expectedVersion != null && version != expectedVersion.Value)
            {
                problems.Add(
                    $"Expected analysis schema version {expectedVersion.Value.ToString(CultureInfo.InvariantCulture)} " +
                    $"but the document is version {actual}.");
                return false;
            }

            if (version > AnalysisDocumentDto.CurrentVersion)
            {
                problems.Add(
                    $"The document is version {actual}, which is newer than this build reads " +
                    $"(version {current}). Upgrade tmforge rather than reading it on older assumptions.");
                return false;
            }

            if (version < AnalysisDocumentDto.CurrentVersion)
            {
                problems.Add(
                    $"The document is version {actual} and no migration to version {current} is " +
                    "available in this build.");
                return false;
            }

            return true;
        }
    }
}
