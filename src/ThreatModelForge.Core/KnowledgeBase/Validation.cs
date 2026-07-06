namespace ThreatModelForge.KnowledgeBase
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>
    /// A recorded validation issue against the model. Serialized with reference semantics so the same
    /// instance can be shared across the graph.
    /// </summary>
    [DataContract(
        IsReference = true,
        Name = "Validation",
        Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.KnowledgeBase")]
    public class Validation
    {
        private List<Guid>? elementGuids;
        private List<object>? items;

        /// <summary>Gets or sets the validation identifier.</summary>
        [DataMember(Name = "Guid")]
#pragma warning disable CA1720 // Identifier contains type name; "Guid" is dictated by the wire schema.
        public Guid Guid { get; set; }
#pragma warning restore CA1720 // Identifier contains type name

        /// <summary>Gets or sets the associated issue identifier.</summary>
        [DataMember(Name = "IssueGuid")]
        public Guid IssueGuid { get; set; }

        /// <summary>Gets the identifiers of the elements this validation refers to.</summary>
        [DataMember(Name = "ElementGuids")]
        public List<Guid> ElementGuids => this.elementGuids ??= new List<Guid>();

        /// <summary>Gets the associated items.</summary>
        [DataMember(Name = "Items")]
        public List<object> Items => this.items ??= new List<object>();

        /// <summary>Gets or sets the message.</summary>
        [DataMember(Name = "Message")]
        public string? Message { get; set; }

        /// <summary>Gets or sets the source description.</summary>
        [DataMember(Name = "Source")]
        public string? Source { get; set; }

        /// <summary>Gets or sets the source element identifier.</summary>
        [DataMember(Name = "SourceGuid")]
        public Guid SourceGuid { get; set; }

        /// <summary>Gets or sets a value indicating whether this validation is enabled.</summary>
        [DataMember(Name = "Enabled")]
        public bool Enabled { get; set; }
    }
}
