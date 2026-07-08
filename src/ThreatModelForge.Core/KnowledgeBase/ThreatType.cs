namespace ThreatModelForge.KnowledgeBase
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using ThreatModelForge.Model;

    /// <summary>
    /// A threat type defined by a knowledge base, including the generation-filter expressions that
    /// decide which interactions it applies to. Persisted in both the <c>.tb7</c> knowledge base and
    /// the copy embedded in a <c>.tm7</c> model.
    /// </summary>
    [DataContract(
        Name = "ThreatType",
        Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.KnowledgeBase")]
    public class ThreatType : Extendable
    {
        private List<ThreatMetaDatum>? propertiesMetaData;
        private GenerationFilters? generationFilters;

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

        /// <summary>
        /// Gets or sets the generation-filter expressions (include/exclude) for this threat type. The
        /// Microsoft Threat Modeling Tool requires this element on every threat type in an embedded
        /// knowledge base, so the getter never returns <see langword="null"/>.
        /// </summary>
        [DataMember(Name = "GenerationFilters")]
        public GenerationFilters GenerationFilters
        {
            get => this.generationFilters ??= new GenerationFilters();
            set => this.generationFilters = value;
        }
    }
}
