namespace ThreatModelForge.Analysis
{
    using System;

    /// <summary>
    /// The effective threat category declared by a rule. Versioned packs namespace the source category
    /// id with the pack id; first-party rules expose their existing STRIDE category through the same
    /// contract while retaining <see cref="Rule.Stride"/> for compatibility.
    /// </summary>
    public sealed class RuleThreatCategory
    {
        /// <summary>Initializes a new instance of the <see cref="RuleThreatCategory"/> class.</summary>
        /// <param name="id">The stable runtime category identifier.</param>
        /// <param name="sourceId">The category id in the defining pack or taxonomy.</param>
        /// <param name="name">The display name.</param>
        /// <param name="shortDescription">The optional short description.</param>
        /// <param name="longDescription">The optional long description.</param>
        public RuleThreatCategory(
            string id,
            string sourceId,
            string name,
            string? shortDescription,
            string? longDescription)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A category id is required.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(sourceId))
            {
                throw new ArgumentException("A source category id is required.", nameof(sourceId));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A category name is required.", nameof(name));
            }

            this.Id = id;
            this.SourceId = sourceId;
            this.Name = name;
            this.ShortDescription = shortDescription;
            this.LongDescription = longDescription;
        }

        /// <summary>Gets the stable runtime category identifier.</summary>
        public string Id { get; }

        /// <summary>Gets the category id in the defining pack or taxonomy.</summary>
        public string SourceId { get; }

        /// <summary>Gets the display name.</summary>
        public string Name { get; }

        /// <summary>Gets the optional short description.</summary>
        public string? ShortDescription { get; }

        /// <summary>Gets the optional long description.</summary>
        public string? LongDescription { get; }

        /// <summary>Creates the effective category for a first-party STRIDE rule.</summary>
        /// <param name="stride">The STRIDE category.</param>
        /// <returns>The effective category.</returns>
        internal static RuleThreatCategory FromStride(StrideCategory stride)
        {
            string sourceId = StrideId(stride);
            string name = StrideName(stride);
            return new RuleThreatCategory(sourceId, sourceId, name, null, null);
        }

        /// <summary>Creates an effective category from one validated pack catalog entry.</summary>
        /// <param name="pack">The containing pack.</param>
        /// <param name="category">The source category definition.</param>
        /// <returns>The namespaced effective category.</returns>
        internal static RuleThreatCategory FromPack(
            RulePackDefinition pack,
            RuleCategoryDefinition category)
        {
            return new RuleThreatCategory(
                RulePackIdentity.CreateEffectiveCategoryId(pack.Id, category.Id),
                category.Id,
                category.Name,
                category.ShortDescription,
                category.LongDescription);
        }

        /// <summary>Returns the canonical MTMT category id for a STRIDE category.</summary>
        /// <param name="stride">The STRIDE category.</param>
        /// <returns>The one-letter category id.</returns>
        internal static string StrideId(StrideCategory stride)
        {
            return stride switch
            {
                StrideCategory.Spoofing => "S",
                StrideCategory.Tampering => "T",
                StrideCategory.Repudiation => "R",
                StrideCategory.InformationDisclosure => "I",
                StrideCategory.DenialOfService => "D",
                StrideCategory.ElevationOfPrivilege => "E",
                _ => string.Empty,
            };
        }

        /// <summary>Returns the canonical display name for a STRIDE category.</summary>
        /// <param name="stride">The STRIDE category.</param>
        /// <returns>The category display name.</returns>
        internal static string StrideName(StrideCategory stride)
        {
            return stride switch
            {
                StrideCategory.InformationDisclosure => "Information Disclosure",
                StrideCategory.DenialOfService => "Denial of Service",
                StrideCategory.ElevationOfPrivilege => "Elevation of Privilege",
                _ => stride.ToString(),
            };
        }
    }
}
