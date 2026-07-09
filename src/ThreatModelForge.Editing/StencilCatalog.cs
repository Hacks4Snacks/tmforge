namespace ThreatModelForge.Editing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;

    /// <summary>
    /// The built-in authoring stencil catalog served to the Studio palette. The catalog is
    /// data-driven: every <c>*.pack.json</c> file embedded under <c>Stencils/</c> contributes a
    /// named pack of stencils, loaded once at startup. Each stencil maps to one of the four DFD
    /// primitives so the existing analysis rules apply unchanged; the stencil identity is carried
    /// into the model as a <c>StencilType</c> custom property.
    /// </summary>
    public static class StencilCatalog
    {
        /// <summary>
        /// Pack load order, which also fixes the palette ordering. Packs not listed here are
        /// appended afterwards, ordered by id.
        /// </summary>
        private static readonly string[] PackOrder = { "generic", "azure", "kubernetes", "identity", "web" };

        /// <summary>Shared, case-insensitive options for reading the pack files.</summary>
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        /// <summary>The embedded packs, loaded once and ordered by <see cref="PackRank"/>.</summary>
        private static readonly IReadOnlyList<PackFile> LoadedPacks = LoadPacks();

        /// <summary>Security-relevant default properties keyed by DFD primitive (from the TMT base types).</summary>
        private static readonly IReadOnlyDictionary<string, Dictionary<string, string>> BaseDefaults = LoadBaseDefaults();

        /// <summary>Gets the full stencil catalog, ordered by pack then by in-pack order.</summary>
        public static IReadOnlyList<StencilDto> All { get; } = LoadedPacks
            .SelectMany(pack => pack.Stencils.Select(entry => new StencilDto
            {
                Id = entry.Id,
                Base = entry.Base,
                Label = entry.Label,
                Category = entry.Category,
                Pack = pack.Id,
                Blurb = entry.Blurb,
                Tags = entry.Tags ?? new List<string>(),
                Defaults = MergeDefaults(entry.Base, entry.Defaults),
            }))
            .ToArray();

        /// <summary>Gets the stencil packs (togglable groups shown in the palette), in load order.</summary>
        public static IReadOnlyList<PackDto> Packs { get; } = LoadedPacks
            .Select(pack => new PackDto { Id = pack.Id, Name = pack.Name, Count = pack.Stencils.Count })
            .ToArray();

        /// <summary>Finds a stencil by its identifier (case-insensitive).</summary>
        /// <param name="id">The stencil identifier (for example, <c>azure-sql</c>).</param>
        /// <returns>The matching stencil, or <see langword="null"/> when no stencil has that id.</returns>
        public static StencilDto? Find(string id)
        {
            return All.FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Reads every embedded stencil pack and orders them for the palette.</summary>
        /// <returns>The ordered packs.</returns>
        private static IReadOnlyList<PackFile> LoadPacks()
        {
            Assembly assembly = typeof(StencilCatalog).Assembly;
            List<PackFile> packs = new List<PackFile>();

            foreach (string resource in assembly.GetManifestResourceNames())
            {
                if (!resource.EndsWith(".pack.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using Stream stream = assembly.GetManifestResourceStream(resource)
                    ?? throw new InvalidOperationException($"Stencil pack resource '{resource}' could not be opened.");
                using StreamReader reader = new StreamReader(stream);
                PackFile pack = JsonSerializer.Deserialize<PackFile>(reader.ReadToEnd(), SerializerOptions)
                    ?? throw new InvalidOperationException($"Stencil pack '{resource}' is empty or invalid.");
                packs.Add(pack);
            }

            return packs
                .OrderBy(pack => PackRank(pack.Id))
                .ThenBy(pack => pack.Id, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>Ranks a pack by its position in <see cref="PackOrder"/>, or last when unlisted.</summary>
        /// <param name="packId">The pack identifier.</param>
        /// <returns>The load-order rank.</returns>
        private static int PackRank(string packId)
        {
            int index = Array.IndexOf(PackOrder, packId);
            return index < 0 ? int.MaxValue : index;
        }

        /// <summary>Reads the embedded per-primitive default property presets, or empty when absent.</summary>
        /// <returns>Default properties keyed by DFD primitive.</returns>
        private static IReadOnlyDictionary<string, Dictionary<string, string>> LoadBaseDefaults()
        {
            Assembly assembly = typeof(StencilCatalog).Assembly;
            string? resource = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith("base-defaults.json", StringComparison.OrdinalIgnoreCase));
            if (resource is null)
            {
                return new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            }

            using Stream stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Base-defaults resource '{resource}' could not be opened.");
            using StreamReader reader = new StreamReader(stream);
            return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(reader.ReadToEnd(), SerializerOptions)
                ?? new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        }

        /// <summary>Merges a primitive's default properties with the stencil's own (the stencil wins).</summary>
        /// <param name="baseId">The DFD primitive the stencil maps to.</param>
        /// <param name="overrides">The stencil's own preset properties, if any.</param>
        /// <returns>The merged default property set.</returns>
        private static IReadOnlyDictionary<string, string> MergeDefaults(string baseId, IReadOnlyDictionary<string, string>? overrides)
        {
            Dictionary<string, string> merged = new Dictionary<string, string>(StringComparer.Ordinal);
            if (BaseDefaults.TryGetValue(baseId, out Dictionary<string, string>? preset))
            {
                foreach (KeyValuePair<string, string> pair in preset)
                {
                    merged[pair.Key] = pair.Value;
                }
            }

            if (overrides is not null)
            {
                foreach (KeyValuePair<string, string> pair in overrides)
                {
                    merged[pair.Key] = pair.Value;
                }
            }

            return merged;
        }

        /// <summary>The on-disk shape of an embedded <c>*.pack.json</c> file.</summary>
        private sealed class PackFile
        {
            /// <summary>Gets or sets the pack identifier (for example, <c>azure</c>).</summary>
            public string Id { get; set; } = string.Empty;

            /// <summary>Gets or sets the human-readable pack name.</summary>
            public string Name { get; set; } = string.Empty;

            /// <summary>Gets or sets the stencils this pack contributes.</summary>
            public List<StencilEntry> Stencils { get; set; } = new List<StencilEntry>();
        }

        /// <summary>The on-disk shape of a single stencil within a pack file.</summary>
        private sealed class StencilEntry
        {
            /// <summary>Gets or sets the stable stencil identifier.</summary>
            public string Id { get; set; } = string.Empty;

            /// <summary>Gets or sets the underlying DFD primitive.</summary>
            public string Base { get; set; } = string.Empty;

            /// <summary>Gets or sets the palette label.</summary>
            public string Label { get; set; } = string.Empty;

            /// <summary>Gets or sets the palette grouping category.</summary>
            public string Category { get; set; } = string.Empty;

            /// <summary>Gets or sets the short description.</summary>
            public string Blurb { get; set; } = string.Empty;

            /// <summary>Gets or sets the optional search tags.</summary>
            public List<string>? Tags { get; set; }

            /// <summary>Gets or sets the optional preset properties applied on placement.</summary>
            public Dictionary<string, string>? Defaults { get; set; }
        }
    }
}
