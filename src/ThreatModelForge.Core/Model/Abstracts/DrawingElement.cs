namespace ThreatModelForge.Model.Abstracts
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Base type for boxed drawing elements — processes, data stores, external entities, and
    /// trust-boundary boxes — anything positioned by a rectangular bounding box with stroke styling.
    /// </summary>
    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.Model.Abstracts")]
    public abstract class DrawingElement : Entity
    {
        /// <summary>Gets or sets the bounding-box height.</summary>
        [DataMember]
        public int Height { get; set; }

        /// <summary>Gets or sets the bounding-box left (x) coordinate.</summary>
        [DataMember]
        public int Left { get; set; }

        /// <summary>Gets or sets the stroke dash pattern; <see langword="null"/> for a solid stroke.</summary>
        [DataMember]
        public object? StrokeDashArray { get; set; }

        /// <summary>Gets or sets the stroke thickness.</summary>
        [DataMember]
        public int StrokeThickness { get; set; }

        /// <summary>Gets or sets the bounding-box top (y) coordinate.</summary>
        [DataMember]
        public int Top { get; set; }

        /// <summary>Gets or sets the bounding-box width.</summary>
        [DataMember]
        public int Width { get; set; }
    }
}
