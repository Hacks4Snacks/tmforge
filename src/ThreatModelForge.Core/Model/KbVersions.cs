namespace ThreatModelForge.Model
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>
    /// A list of knowledge-base version strings, serialized under the empty ("") data-contract
    /// namespace to match the on-disk schema.
    /// </summary>
    [CollectionDataContract(Name = "KbVersion", ItemName = "Version", Namespace = "")]
    public class KbVersions : List<string>
    {
    }
}
