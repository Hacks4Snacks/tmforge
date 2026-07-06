namespace ThreatModelForge.Model.Abstracts
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Base type for connectors and boundary lines — elements defined by a source point, a target
    /// point, and a single curve handle rather than a bounding box.
    /// </summary>
    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.Model.Abstracts")]
    public abstract class LineElement : Entity
    {
        /// <summary>Gets or sets the x coordinate of the curve handle.</summary>
        [DataMember]
        public int HandleX { get; set; }

        /// <summary>Gets or sets the y coordinate of the curve handle.</summary>
        [DataMember]
        public int HandleY { get; set; }

        /// <summary>Gets or sets the name of the source port.</summary>
        [DataMember]
        public string? PortSource { get; set; }

        /// <summary>Gets or sets the name of the target port.</summary>
        [DataMember]
        public string? PortTarget { get; set; }

        /// <summary>Gets or sets the identifier of the element the source end attaches to.</summary>
        [DataMember]
        public Guid SourceGuid { get; set; }

        /// <summary>Gets or sets the source endpoint x coordinate.</summary>
        [DataMember]
        public int SourceX { get; set; }

        /// <summary>Gets or sets the source endpoint y coordinate.</summary>
        [DataMember]
        public int SourceY { get; set; }

        /// <summary>Gets or sets the identifier of the element the target end attaches to.</summary>
        [DataMember]
        public Guid TargetGuid { get; set; }

        /// <summary>Gets or sets the target endpoint x coordinate.</summary>
        [DataMember]
        public int TargetX { get; set; }

        /// <summary>Gets or sets the target endpoint y coordinate.</summary>
        [DataMember]
        public int TargetY { get; set; }
    }
}
