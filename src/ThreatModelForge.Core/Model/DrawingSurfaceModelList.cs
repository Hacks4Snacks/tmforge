namespace ThreatModelForge.Model
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>
    /// The ordered set of diagrams in a threat model. Declared as a reference-preserving collection
    /// contract (<c>IsReference</c>) so that shared element instances serialize with
    /// <c>z:Id</c>/<c>z:Ref</c>, matching the on-disk format.
    /// </summary>
    [CollectionDataContract(
        ItemName = "DrawingSurfaceModel",
        Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.Model",
        IsReference = true)]
    public class DrawingSurfaceModelList : List<DrawingSurfaceModel>
    {
    }
}
