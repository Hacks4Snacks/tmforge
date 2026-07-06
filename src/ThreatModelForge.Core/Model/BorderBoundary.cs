namespace ThreatModelForge.Model
{
    using System.Runtime.Serialization;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// A trust boundary drawn as a rectangular region enclosing other elements.
    /// </summary>
    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.Model")]
    public class BorderBoundary : DrawingElement
    {
    }
}
