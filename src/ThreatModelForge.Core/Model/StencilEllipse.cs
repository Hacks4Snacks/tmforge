namespace ThreatModelForge.Model
{
    using System.Runtime.Serialization;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// An ellipse-shaped element, conventionally used to represent a process.
    /// </summary>
    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.Model")]
    public class StencilEllipse : DrawingElement
    {
    }
}
