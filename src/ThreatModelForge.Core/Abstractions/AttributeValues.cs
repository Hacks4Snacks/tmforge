namespace ThreatModelForge.Abstractions
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>
    /// A list of attribute values, serialized as a collection of <c>Value</c> items.
    /// </summary>
    [CollectionDataContract(
        ItemName = "Value",
        Name = "AttributeValues",
        Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.Interfaces")]
    public class AttributeValues : List<string>
    {
    }
}
