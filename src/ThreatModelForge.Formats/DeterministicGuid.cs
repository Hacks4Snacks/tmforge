namespace ThreatModelForge.Formats
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Derives stable internal identifiers from the ids an author chooses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The engine keys everything on <see cref="Guid"/>, but canonical model JSON lets an author use
    /// any string as an id — <c>web-app</c>, <c>s1</c>, an alias from a manifest. Minting a fresh guid
    /// for those on every load makes anything derived from the guid churn between runs: threat
    /// register keys stop matching their triage after a round trip, and the guid embedded in a finding
    /// message changes even though nothing about the model did.
    /// </para>
    /// <para>
    /// Deriving the guid from the author's id instead makes those stable, and makes two files that
    /// name the same element agree \u2014 which is what the structural diff and three-way merge match on.
    /// The derivation is a name-based (RFC 4122 version 5 style) hash, namespaced so an element and a
    /// page that happen to share an id do not collide.
    /// </para>
    /// </remarks>
    public static class DeterministicGuid
    {
        private const string ElementNamespace = "tmforge-alias:";

        private const string PageNamespace = "tmforge-page:";

        /// <summary>
        /// Derives the identifier for an element or flow. This is the same derivation the authoring
        /// aliases use, so a model built from a manifest and the same model read back from JSON agree
        /// on element identity.
        /// </summary>
        /// <param name="id">The author's element id or alias.</param>
        /// <returns>A stable identifier for the id.</returns>
        public static Guid FromElementId(string id) => Derive(ElementNamespace, id);

        /// <summary>Derives the identifier for a page.</summary>
        /// <param name="id">The author's page id.</param>
        /// <returns>A stable identifier for the id.</returns>
        public static Guid FromPageId(string id) => Derive(PageNamespace, id);

        private static Guid Derive(string space, string id)
        {
            _ = id ?? throw new ArgumentNullException(nameof(id));

            byte[] guidBytes = new byte[16];
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(space + id));
                Array.Copy(hash, guidBytes, 16);
            }

            // Stamp the RFC 4122 version (5, name-based) and variant bits so the id is well-formed.
            guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x50);
            guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
            return new Guid(guidBytes);
        }
    }
}
