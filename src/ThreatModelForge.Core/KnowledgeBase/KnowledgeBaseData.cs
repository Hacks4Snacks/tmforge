namespace ThreatModelForge.KnowledgeBase
{
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization;
    using ThreatModelForge.Model;

    /// <summary>
    /// The contents of a knowledge base (<c>.tb7</c>): its manifest, threat metadata, element types
    /// (generic and standard), threat categories, and threat types. Also embedded — by reference —
    /// inside a <c>.tm7</c> model.
    /// </summary>
    [DataContract(
        IsReference = true,
        Name = "KnowledgeBase",
        Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.KnowledgeBase")]
    public class KnowledgeBaseData
    {
        private List<ElementType>? genericElements;
        private List<ElementType>? standardElements;
        private List<ThreatCategory>? threatCategories;
        private List<ThreatType>? threatTypes;

        /// <summary>Gets or sets the knowledge-base manifest.</summary>
        [DataMember(Name = "Manifest")]
        public Manifest? Manifest { get; set; }

        /// <summary>Gets or sets the custom threat-property definitions.</summary>
        [DataMember(Name = "ThreatMetaData")]
        public ThreatMetaData? ThreatMetaData { get; set; }

        /// <summary>Gets the generic element types.</summary>
        [DataMember(Name = "GenericElements")]
        public List<ElementType> GenericElements => this.genericElements ??= new List<ElementType>();

        /// <summary>Gets the standard element types.</summary>
        [DataMember(Name = "StandardElements")]
        public List<ElementType> StandardElements => this.standardElements ??= new List<ElementType>();

        /// <summary>Gets the threat categories.</summary>
        [DataMember(Name = "ThreatCategories")]
        public List<ThreatCategory> ThreatCategories => this.threatCategories ??= new List<ThreatCategory>();

        /// <summary>Gets the threat types.</summary>
        [DataMember(Name = "ThreatTypes")]
        public List<ThreatType> ThreatTypes => this.threatTypes ??= new List<ThreatType>();

        /// <summary>Loads a knowledge base from a <c>.tb7</c> file.</summary>
        /// <param name="path">The file path.</param>
        /// <returns>The loaded <see cref="KnowledgeBaseData"/>.</returns>
        public static KnowledgeBaseData Load(string path)
        {
            using Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            return Load(stream);
        }

        /// <summary>Loads a knowledge base from a stream.</summary>
        /// <param name="stream">The source stream.</param>
        /// <returns>The loaded <see cref="KnowledgeBaseData"/>.</returns>
        public static KnowledgeBaseData Load(Stream stream)
        {
            return TemplateSerializer.ReadObject(stream);
        }

        /// <summary>Saves this knowledge base to a <c>.tb7</c> file.</summary>
        /// <param name="path">The destination path.</param>
        public void Save(string path)
        {
            using Stream stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            this.Save(stream);
        }

        /// <summary>Saves this knowledge base to a stream.</summary>
        /// <param name="stream">The destination stream.</param>
        public void Save(Stream stream)
        {
            TemplateSerializer.WriteObject(stream, this);
        }
    }
}
