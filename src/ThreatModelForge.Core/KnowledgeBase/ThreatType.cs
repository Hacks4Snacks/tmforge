namespace ThreatModelForge.KnowledgeBase
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using ThreatModelForge.Model;

    /// <summary>
    /// A threat type defined by a knowledge base. Its generation-filter expressions are read from the
    /// <c>.tb7</c> but are not persisted into the <c>.tm7</c> snapshot.
    /// </summary>
    [DataContract(
        Name = "ThreatType",
        Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.KnowledgeBase")]
    public class ThreatType : Extendable
    {
        private List<ThreatMetaDatum>? propertiesMetaData;

        /// <summary>Gets or sets the identifier.</summary>
        [DataMember(Name = "Id")]
        public string? Id { get; set; }

        /// <summary>Gets or sets the short title.</summary>
        [DataMember(Name = "ShortTitle")]
        public string? ShortTitle { get; set; }

        /// <summary>Gets or sets the category identifier.</summary>
        [DataMember(Name = "Category")]
        public string? Category { get; set; }

        /// <summary>Gets or sets the related category identifier.</summary>
        [DataMember(Name = "RelatedCategory")]
        public string? RelatedCategory { get; set; }

        /// <summary>Gets or sets the description.</summary>
        [DataMember(Name = "Description")]
        public string? Description { get; set; }

        /// <summary>Gets the metadata describing this threat type's custom properties.</summary>
        [DataMember(Name = "PropertiesMetaData")]
        public List<ThreatMetaDatum> PropertiesMetaData => this.propertiesMetaData ??= new List<ThreatMetaDatum>();

        /// <summary>Gets or sets the include generation-filter expression (TB7 only).</summary>
        [IgnoreDataMember]
        public string? IncludeGenerationFilter { get; set; }

        /// <summary>Gets or sets the exclude generation-filter expression (TB7 only).</summary>
        [IgnoreDataMember]
        public string? ExcludeGenerationFilter { get; set; }
    }
}
