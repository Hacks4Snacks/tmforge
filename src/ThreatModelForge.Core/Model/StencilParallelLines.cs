namespace ThreatModelForge.Model
{
    using System.Runtime.Serialization;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// A pair-of-parallel-lines element, conventionally used to represent a data store.
    /// </summary>
    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.Model")]
    public class StencilParallelLines : DrawingElement
    {
    }
}
