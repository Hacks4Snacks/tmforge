namespace ThreatModelForge.KnowledgeBase
{
    using System.Runtime.Serialization;

    /// <summary>
    /// A stencil connection constraint for a knowledge-base element type.
    /// </summary>
    [DataContract(
        Name = "StencilConstraint",
        Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.KnowledgeBase")]
    public class StencilConstraint : Extendable
    {
        /// <summary>Gets or sets the selected stencil type.</summary>
        [DataMember(Name = "SelectedStencilType")]
        public string? SelectedStencilType { get; set; }

        /// <summary>Gets or sets the selected stencil connection.</summary>
        [DataMember(Name = "SelectedStencilConnection")]
        public string? SelectedStencilConnection { get; set; }
    }
}
