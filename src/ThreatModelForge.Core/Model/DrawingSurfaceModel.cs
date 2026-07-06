namespace ThreatModelForge.Model
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// A single diagram page: the boxed elements (<see cref="Borders"/>) and the connectors and
    /// boundary lines (<see cref="Lines"/>) drawn on it, each keyed by element identifier.
    /// </summary>
    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.Model")]
    public class DrawingSurfaceModel : Entity
    {
        private Dictionary<Guid, object>? borders;
        private Dictionary<Guid, object>? lines;

        /// <summary>
        /// Gets the boxed elements (components and boundary boxes) on this diagram, keyed by identifier.
        /// </summary>
        [DataMember]
        public IDictionary<Guid, object> Borders => this.borders ??= new Dictionary<Guid, object>();

        /// <summary>
        /// Gets or sets the diagram title.
        /// </summary>
        [DataMember]
        public string? Header { get; set; }

        /// <summary>
        /// Gets the connectors and boundary lines on this diagram, keyed by identifier.
        /// </summary>
        [DataMember]
        public IDictionary<Guid, object> Lines => this.lines ??= new Dictionary<Guid, object>();

        /// <summary>
        /// Gets or sets the zoom factor last used when viewing this diagram.
        /// </summary>
        [DataMember]
        public float Zoom { get; set; }
    }
}
