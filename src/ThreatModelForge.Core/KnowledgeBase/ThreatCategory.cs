namespace ThreatModelForge.KnowledgeBase
{
    using System.Runtime.Serialization;

    /// <summary>
    /// A category grouping related threat types within a knowledge base.
    /// </summary>
    [DataContract(
        Name = "ThreatCategory",
        Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.KnowledgeBase")]
    public class ThreatCategory : Extendable
    {
        /// <summary>Gets or sets the category name.</summary>
        [DataMember(Name = "Name")]
        public string? Name { get; set; }

        /// <summary>Gets or sets the identifier.</summary>
        [DataMember(Name = "Id")]
        public string? Id { get; set; }

        /// <summary>Gets or sets the short description.</summary>
        [DataMember(Name = "ShortDescription")]
        public string? ShortDescription { get; set; }

        /// <summary>Gets or sets the long description.</summary>
        [DataMember(Name = "LongDescription")]
        public string? LongDescription { get; set; }
    }
}
