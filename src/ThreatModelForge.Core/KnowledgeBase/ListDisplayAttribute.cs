namespace ThreatModelForge.KnowledgeBase
{
    using System.Runtime.Serialization;

    /// <summary>
    /// A single-selection list display property.
    /// </summary>
    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.KnowledgeBase")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix; the name mirrors the wire schema.
    public class ListDisplayAttribute : DisplayAttribute
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    {
        /// <summary>Gets or sets the index of the selected item.</summary>
        [DataMember]
        public int SelectedIndex { get; set; }
    }
}
