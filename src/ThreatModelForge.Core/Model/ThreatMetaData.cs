namespace ThreatModelForge.Model
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>
    /// Describes the custom threat properties a model tracks, and whether the priority and status
    /// fields are in use.
    /// </summary>
    [DataContract(
        Name = "ThreatMetaData",
        Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.Model")]
    public class ThreatMetaData
    {
        private List<ThreatMetaDatum>? propertiesMetaData;

        /// <summary>Gets or sets a value indicating whether the priority field is used.</summary>
        [DataMember(Name = "IsPriorityUsed", Order = 1)]
        public bool IsPriorityUsed { get; set; }

        /// <summary>Gets or sets a value indicating whether the status field is used.</summary>
        [DataMember(Name = "IsStatusUsed", Order = 2)]
        public bool IsStatusUsed { get; set; }

        /// <summary>Gets the metadata describing each custom threat property.</summary>
        [DataMember(Name = "PropertiesMetaData", Order = 3)]
        public List<ThreatMetaDatum> PropertiesMetaData => this.propertiesMetaData ??= new List<ThreatMetaDatum>();
    }
}
