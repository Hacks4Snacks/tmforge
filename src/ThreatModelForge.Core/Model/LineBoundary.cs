namespace ThreatModelForge.Model
{
    using System.Runtime.Serialization;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// A trust boundary drawn as a line between two points.
    /// </summary>
    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.Model")]
    public class LineBoundary : LineElement
    {
    }
}
