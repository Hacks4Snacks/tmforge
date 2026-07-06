namespace ThreatModelForge.KnowledgeBase
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Base type for knowledge-base entities that a downstream (extension) knowledge base may add to
    /// or override.
    /// </summary>
    [DataContract(
        Name = "Extendable",
        Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.KnowledgeBase")]
    public abstract class Extendable
    {
        /// <summary>Gets or sets a value indicating whether this entity comes from an extension.</summary>
        [DataMember(Name = "IsExtension")]
        public bool IsExtension { get; set; }
    }
}
