namespace ThreatModelForge.Model
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>
    /// Metadata for a single custom threat property: its name, label, allowed values, and type.
    /// </summary>
    [DataContract(
        Name = "ThreatMetaDatum",
        Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.Model")]
    public class ThreatMetaDatum
    {
        private List<string>? values;

        /// <summary>Gets or sets the property name.</summary>
        [DataMember(IsRequired = true, Name = "Name", Order = 1)]
        public string? Name { get; set; }

        /// <summary>Gets or sets the display label.</summary>
        [DataMember(IsRequired = true, Name = "Label", Order = 2)]
        public string? Label { get; set; }

        /// <summary>Gets or sets a value indicating whether this property is hidden from the UI.</summary>
        [DataMember(IsRequired = true, Name = "HideFromUI", Order = 3)]
        public bool HideFromUI { get; set; }

        /// <summary>Gets the set of allowed values.</summary>
        [DataMember(IsRequired = false, Name = "Values", Order = 4)]
        public List<string> Values => this.values ??= new List<string>();

        /// <summary>Gets or sets the property description.</summary>
        [DataMember(EmitDefaultValue = false, Name = "Description", Order = 5)]
        public string? Description { get; set; }

        /// <summary>Gets or sets the property identifier.</summary>
        [DataMember(Name = "Id", Order = 6)]
        public string? Id { get; set; }

        /// <summary>Gets or sets the attribute-type discriminator.</summary>
        [DataMember(Name = "AttributeType", Order = 7)]
        public int AttributeType { get; set; }
    }
}
