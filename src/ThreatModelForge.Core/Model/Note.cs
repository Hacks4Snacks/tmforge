namespace ThreatModelForge.Model
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// A free-text note recorded against the threat model.
    /// </summary>
    [DataContract(
        Name = "Note",
        Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.Model")]
    public class Note
    {
        /// <summary>Gets or sets the note identifier.</summary>
        [DataMember]
        public int Id { get; set; }

        /// <summary>Gets or sets the note text.</summary>
        [DataMember]
        public string? Message { get; set; }

        /// <summary>Gets or sets the time the note was recorded.</summary>
        [DataMember]
        public DateTime Date { get; set; }

        /// <summary>Gets or sets the author of the note.</summary>
        [DataMember]
        public string? AddedBy { get; set; }
    }
}
