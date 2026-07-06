namespace ThreatModelForge.KnowledgeBase
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Identifying metadata for a knowledge base: its name, id, version, and author.
    /// </summary>
    [DataContract(
        Name = "Manifest",
        Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.KnowledgeBase")]
    public class Manifest
    {
        /// <summary>Gets or sets the knowledge-base name.</summary>
        [DataMember(Name = "Name")]
        public string? Name { get; set; }

        /// <summary>Gets or sets the knowledge-base identifier.</summary>
        [DataMember(Name = "Id")]
        public Guid Id { get; set; }

        /// <summary>Gets or sets the version string.</summary>
        [DataMember(Name = "Version")]
        public string? Version { get; set; }

        /// <summary>Gets or sets the author.</summary>
        [DataMember(Name = "Author")]
        public string? Author { get; set; }
    }
}
