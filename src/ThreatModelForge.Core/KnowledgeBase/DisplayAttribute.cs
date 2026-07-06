namespace ThreatModelForge.KnowledgeBase
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Base type for the polymorphic display properties attached to model elements and knowledge-base
    /// attributes. Serialized inside a <c>Properties</c> array with an <c>i:type</c> discriminator, so
    /// each concrete subtype is registered here as a <see cref="KnownTypeAttribute"/>.
    /// </summary>
    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.KnowledgeBase")]
    [KnownType(typeof(CustomStringDisplayAttribute))]
    [KnownType(typeof(StringDisplayAttribute))]
    [KnownType(typeof(HeaderDisplayAttribute))]
    [KnownType(typeof(BooleanDisplayAttribute))]
    [KnownType(typeof(ListDisplayAttribute))]
    [KnownType(typeof(StaticListDisplayAttribute))]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix; the name mirrors the wire schema.
    public abstract class DisplayAttribute
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    {
        /// <summary>Gets or sets the label shown for this property.</summary>
        [DataMember]
        public string? DisplayName { get; set; }

        /// <summary>Gets or sets the internal property name.</summary>
        [DataMember]
        public string? Name { get; set; }

        /// <summary>Gets or sets the property value.</summary>
        [DataMember]
        public object? Value { get; set; }
    }
}
