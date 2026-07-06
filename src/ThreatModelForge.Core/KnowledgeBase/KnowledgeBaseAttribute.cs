namespace ThreatModelForge.KnowledgeBase
{
    using System.Runtime.Serialization;
    using ThreatModelForge.Abstractions;

    /// <summary>How an attribute's set of values is populated.</summary>
    public enum AttributeMode
    {
        /// <summary>The value set is fixed.</summary>
        Static,

        /// <summary>The value set is computed at runtime.</summary>
        Dynamic,
    }

    /// <summary>The kind of a knowledge-base attribute.</summary>
    public enum AttributeType
    {
        /// <summary>A list-valued attribute.</summary>
        List,
    }

    /// <summary>How an attribute is inherited by extension knowledge bases.</summary>
    public enum AttributeInheritance
    {
        /// <summary>The attribute may be overridden by an extension.</summary>
        Virtual,

        /// <summary>The attribute is newly introduced by an extension.</summary>
        New,
    }

    /// <summary>
    /// A custom attribute defined on a knowledge-base element type.
    /// </summary>
    [DataContract(
        Name = "Attribute",
        Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.KnowledgeBase")]
    public class KnowledgeBaseAttribute : Extendable
    {
        private AttributeValues? attributeValues;

        /// <summary>Gets or sets the attribute name.</summary>
        [DataMember(Name = "Name")]
        public string? Name { get; set; }

        /// <summary>Gets or sets the display name.</summary>
        [DataMember(Name = "DisplayName")]
        public string? DisplayName { get; set; }

        /// <summary>Gets or sets how the value set is populated.</summary>
        [DataMember(Name = "Mode")]
        public AttributeMode Mode { get; set; }

        /// <summary>Gets or sets the attribute kind.</summary>
        [DataMember(Name = "Type")]
        public AttributeType Type { get; set; }

        /// <summary>Gets or sets the inheritance mode.</summary>
        [DataMember(Name = "Inheritance")]
        public AttributeInheritance Inheritance { get; set; }

        /// <summary>Gets the allowed values for this attribute.</summary>
        [DataMember(Name = "AttributeValues")]
        public AttributeValues AttributeValues => this.attributeValues ??= new AttributeValues();

        /// <summary>Gets or sets a value indicating whether the attribute is inherited (TB7 only, not persisted to TM7).</summary>
        [IgnoreDataMember]
        public bool IsInherited { get; set; }

        /// <summary>Gets or sets a value indicating whether the attribute is overridden (TB7 only, not persisted to TM7).</summary>
        [IgnoreDataMember]
        public bool IsOverrided { get; set; }

        /// <summary>Gets or sets the attribute identifier (TB7 only, not persisted to TM7).</summary>
        [IgnoreDataMember]
        public string? Id { get; set; }
    }
}
