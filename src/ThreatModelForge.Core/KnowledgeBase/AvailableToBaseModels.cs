namespace ThreatModelForge.KnowledgeBase
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>
    /// The set of base-model identifiers an element type is available to, serialized as a list of
    /// <c>BaseModelId</c> items.
    /// </summary>
    [CollectionDataContract(ItemName = "BaseModelId", Name = "AvailableToBaseModels")]
    public class AvailableToBaseModels : List<string>
    {
    }
}
