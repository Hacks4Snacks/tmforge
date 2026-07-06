namespace ThreatModelForge.KnowledgeBase
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>
    /// Describes an element type offered by a knowledge base — its identity, visual representation,
    /// custom attributes, and stencil constraints.
    /// </summary>
    [DataContract(
        Name = "ElementType",
        Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.KnowledgeBase")]
    public class ElementType : Extendable
    {
        private List<KnowledgeBaseAttribute>? attributes;
        private AvailableToBaseModels? availableToBaseModels;
        private List<StencilConstraint>? stencilConstraints;

        /// <summary>Gets or sets the display name.</summary>
        [DataMember(Name = "Name")]
        public string? Name { get; set; }

        /// <summary>Gets or sets the identifier.</summary>
        [DataMember(Name = "Id")]
        public string? Id { get; set; }

        /// <summary>Gets or sets the description.</summary>
        [DataMember(Name = "Description")]
        public string? Description { get; set; }

        /// <summary>Gets or sets the parent element-type identifier.</summary>
        [DataMember(Name = "ParentId")]
        public string? ParentId { get; set; }

        /// <summary>Gets or sets the image source.</summary>
        [DataMember(Name = "ImageSource")]
        public string? ImageSource { get; set; }

        /// <summary>Gets or sets the embedded image stream.</summary>
        [DataMember(Name = "ImageStream")]
        public string? ImageStream { get; set; }

        /// <summary>Gets or sets a value indicating whether this element type is hidden.</summary>
        [DataMember(Name = "Hidden")]
        public bool Hidden { get; set; }

        /// <summary>Gets or sets the behavior descriptor.</summary>
        [DataMember(Name = "Behavior")]
        public string? Behavior { get; set; }

        /// <summary>Gets or sets the shape descriptor.</summary>
        [DataMember(Name = "Shape")]
        public string? Shape { get; set; }

        /// <summary>Gets or sets the visual representation.</summary>
        [DataMember(Name = "Representation")]
        public ElementVisualRepresentation Representation { get; set; }

        /// <summary>Gets or sets the stroke thickness.</summary>
        [DataMember(EmitDefaultValue = false, Name = "StrokeThickness")]
        public double StrokeThickness { get; set; }

        /// <summary>Gets or sets the stroke dash pattern.</summary>
        [DataMember(EmitDefaultValue = false, Name = "StrokeDashArray")]
        public string? StrokeDashArray { get; set; }

        /// <summary>Gets or sets the image location.</summary>
        [DataMember(Name = "ImageLocation")]
        public string? ImageLocation { get; set; }

        /// <summary>Gets the custom attributes defined on this element type.</summary>
        [DataMember(Name = "Attributes")]
        public List<KnowledgeBaseAttribute> Attributes => this.attributes ??= new List<KnowledgeBaseAttribute>();

        /// <summary>Gets the base models this element type is available to.</summary>
        [DataMember(Name = "AvailableToBaseModels")]
        public AvailableToBaseModels AvailableToBaseModels => this.availableToBaseModels ??= new AvailableToBaseModels();

        /// <summary>Gets the stencil constraints for this element type.</summary>
        [DataMember(Name = "StencilConstraints")]
        public List<StencilConstraint> StencilConstraints => this.stencilConstraints ??= new List<StencilConstraint>();
    }
}
