namespace ThreatModelForge.KnowledgeBase
{
    using System.Runtime.Serialization;

    /// <summary>
    /// A free-text (string) display property.
    /// </summary>
    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.KnowledgeBase")]
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix; the name mirrors the wire schema.
    public class StringDisplayAttribute : DisplayAttribute
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix
    {
    }
}
