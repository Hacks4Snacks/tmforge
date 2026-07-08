namespace ThreatModelForge.KnowledgeBase
{
    using System.Runtime.Serialization;

    /// <summary>
    /// The generation-filter expressions for a <see cref="ThreatType"/>: the <c>Include</c> and
    /// <c>Exclude</c> target expressions that decide which interactions the threat is generated for.
    /// Persisted in both the <c>.tb7</c> knowledge base and the copy embedded in a <c>.tm7</c> model;
    /// the Microsoft Threat Modeling Tool requires this element to be present on every threat type.
    /// </summary>
    [DataContract(
        Name = "GenerationFilters",
        Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.KnowledgeBase")]
    public class GenerationFilters
    {
        /// <summary>Gets or sets the exclude expression.</summary>
        [DataMember(Name = "Exclude")]
        public string Exclude { get; set; } = string.Empty;

        /// <summary>Gets or sets the include expression.</summary>
        [DataMember(Name = "Include")]
        public string Include { get; set; } = string.Empty;
    }
}
