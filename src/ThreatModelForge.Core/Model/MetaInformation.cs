namespace ThreatModelForge.Model
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Free-text metadata describing the modeled system (owner, reviewer, assumptions, and so on).
    /// </summary>
    [DataContract(
        Name = "MetaInformation",
        Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.Model")]
    public class MetaInformation
    {
        /// <summary>Gets or sets the recorded assumptions.</summary>
        [DataMember]
        public string? Assumptions { get; set; }

        /// <summary>Gets or sets the contributors.</summary>
        [DataMember]
        public string? Contributors { get; set; }

        /// <summary>Gets or sets the external dependencies.</summary>
        [DataMember]
        public string? ExternalDependencies { get; set; }

        /// <summary>Gets or sets the high-level system description.</summary>
        [DataMember]
        public string? HighLevelSystemDescription { get; set; }

        /// <summary>Gets or sets the model owner.</summary>
        [DataMember]
        public string? Owner { get; set; }

        /// <summary>Gets or sets the reviewer.</summary>
        [DataMember]
        public string? Reviewer { get; set; }

        /// <summary>Gets or sets the threat model name.</summary>
        [DataMember]
        public string? ThreatModelName { get; set; }
    }
}
