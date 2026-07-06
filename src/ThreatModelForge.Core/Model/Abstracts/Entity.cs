namespace ThreatModelForge.Model.Abstracts
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>
    /// Base type for every node stored on a drawing surface. The data-contract layout mirrors the
    /// observable <c>.tm7</c> schema: an optional generic type id, a unique
    /// identifier, an optional concrete type id, and an open list of display properties.
    /// </summary>
    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.Model.Abstracts")]
    public abstract class Entity
    {
        private List<object>? properties;

        /// <summary>
        /// Gets or sets the identifier of the generic element this node is based on.
        /// </summary>
        [DataMember]
        public string? GenericTypeId { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of this node.
        /// </summary>
        [DataMember]
#pragma warning disable CA1720 // Identifier contains type name; "Guid" is dictated by the wire schema.
        public Guid Guid { get; set; }
#pragma warning restore CA1720 // Identifier contains type name

        /// <summary>
        /// Gets or sets the identifier of the concrete element type.
        /// </summary>
        [DataMember]
        public string? TypeId { get; set; }

        /// <summary>
        /// Gets the display properties attached to this node, serialized as an array of polymorphic
        /// display attributes.
        /// </summary>
        [DataMember]
        public IList<object> Properties => this.properties ??= new List<object>();
    }
}
