namespace ThreatModelForge.Analysis
{
    using System;

    /// <summary>
    /// A first-class reference from a <see cref="Rule"/> (and the threat projected from its finding) to
    /// an external catalog entry — CWE, CAPEC, or MITRE ATT&amp;CK. References are typed rather than
    /// free text because external catalogs are a graph of cross-referenced identifiers. Construct with
    /// <see cref="Cwe"/>, <see cref="Capec"/>, or <see cref="Attack"/>.
    /// </summary>
    public sealed class ThreatReference
    {
        /// <summary>Initializes a new instance of the <see cref="ThreatReference"/> class.</summary>
        /// <param name="catalog">The catalog (for example <c>CWE</c>, <c>CAPEC</c>, <c>ATTACK</c>).</param>
        /// <param name="id">The identifier within the catalog (for example <c>CWE-287</c>).</param>
        /// <param name="url">An optional canonical URL for the referenced entry.</param>
        public ThreatReference(string catalog, string id, string? url = null)
        {
            this.Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.Id = id ?? throw new ArgumentNullException(nameof(id));
            this.Url = url;
        }

        /// <summary>Gets the catalog the reference belongs to (for example <c>CWE</c>, <c>CAPEC</c>, <c>ATTACK</c>).</summary>
        public string Catalog { get; }

        /// <summary>Gets the identifier within the catalog (for example <c>CWE-287</c>, <c>CAPEC-151</c>).</summary>
        public string Id { get; }

        /// <summary>Gets an optional canonical URL for the referenced entry.</summary>
        public string? Url { get; }

        /// <summary>Creates a CWE (Common Weakness Enumeration) reference.</summary>
        /// <param name="id">The numeric CWE identifier.</param>
        /// <returns>A new reference.</returns>
        public static ThreatReference Cwe(int id) =>
            new ThreatReference("CWE", $"CWE-{id}", $"https://cwe.mitre.org/data/definitions/{id}.html");

        /// <summary>Creates a CAPEC (Common Attack Pattern Enumeration and Classification) reference.</summary>
        /// <param name="id">The numeric CAPEC identifier.</param>
        /// <returns>A new reference.</returns>
        public static ThreatReference Capec(int id) =>
            new ThreatReference("CAPEC", $"CAPEC-{id}", $"https://capec.mitre.org/data/definitions/{id}.html");

        /// <summary>Creates a MITRE ATT&amp;CK technique reference.</summary>
        /// <param name="techniqueId">The technique identifier (for example <c>T1190</c>).</param>
        /// <returns>A new reference.</returns>
        public static ThreatReference Attack(string techniqueId) =>
            new ThreatReference("ATTACK", techniqueId, $"https://attack.mitre.org/techniques/{techniqueId}/");
    }
}
