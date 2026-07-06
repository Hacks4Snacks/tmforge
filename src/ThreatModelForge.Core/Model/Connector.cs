namespace ThreatModelForge.Model
{
    using System.Runtime.Serialization;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// A data flow connecting two elements on a drawing surface.
    /// </summary>
    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.Model")]
    public class Connector : LineElement
    {
    }
}
