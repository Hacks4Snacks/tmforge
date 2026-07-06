namespace ThreatModelForge.Model
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization;
    using ThreatModelForge.KnowledgeBase;

    /// <summary>
    /// An in-memory Threat Modeling Tool document (<c>.tm7</c>). Reads and writes byte-for-byte
    /// compatible files with <see cref="DataContractSerializer"/> over the model graph.
    /// </summary>
    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/ThreatModeling.Model")]
    public class ThreatModel
    {
        private static readonly DataContractSerializer Serializer = CreateSerializer();

        private DrawingSurfaceModelList? drawingSurfaceList;
        private List<Note>? notes;
        private Dictionary<string, Threat>? allThreats;
        private List<Validation>? validations;

        /// <summary>Gets the diagrams that make up this model.</summary>
        [DataMember(IsRequired = true, Name = "DrawingSurfaceList", Order = 1)]
        public DrawingSurfaceModelList DrawingSurfaceList => this.drawingSurfaceList ??= new DrawingSurfaceModelList();

        /// <summary>Gets or sets the system metadata.</summary>
        [DataMember(Name = "MetaInformation", Order = 2)]
        public MetaInformation? MetaInformation { get; set; }

        /// <summary>Gets the notes recorded against this model.</summary>
        [DataMember(Name = "Notes", Order = 4)]
        public List<Note> Notes => this.notes ??= new List<Note>();

        /// <summary>Gets every threat instance, keyed by its composite instance key.</summary>
        [DataMember(Name = "ThreatInstances", Order = 5)]
        public Dictionary<string, Threat> AllThreatsDictionary =>
            this.allThreats ??= new Dictionary<string, Threat>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets or sets a value indicating whether threat generation is enabled for this model.</summary>
        [DataMember(Name = "ThreatGenerationEnabled", Order = 6)]
        public bool? ThreatGenerationEnabled { get; set; }

        /// <summary>Gets the validation results stored with this model.</summary>
        [DataMember(Name = "Validations", Order = 7)]
        public List<Validation> Validations => this.validations ??= new List<Validation>();

        /// <summary>Gets or sets the file-format version string.</summary>
        [DataMember(Name = "Version", Order = 8)]
        public string? Version { get; set; }

        /// <summary>Gets or sets the embedded knowledge base, if any.</summary>
        [DataMember(Name = "KnowledgeBase", Order = 9)]
        public KnowledgeBaseData? KnowledgeBase { get; set; }

        /// <summary>Gets or sets the profile data, if any.</summary>
        [DataMember(Name = "Profile", Order = 10)]
        public ProfileData? Profile { get; set; }

        /// <summary>
        /// Loads a threat model from a <c>.tm7</c> file.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>The loaded <see cref="ThreatModel"/>.</returns>
        public static ThreatModel Load(string path)
        {
            using Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            return Load(stream);
        }

        /// <summary>
        /// Loads a threat model from a stream.
        /// </summary>
        /// <param name="stream">The source stream.</param>
        /// <returns>The loaded <see cref="ThreatModel"/>.</returns>
        public static ThreatModel Load(Stream stream)
        {
            return (ThreatModel)Serializer.ReadObject(stream);
        }

        /// <summary>
        /// Saves this model to a <c>.tm7</c> file.
        /// </summary>
        /// <param name="path">The destination path.</param>
        public void Save(string path)
        {
            using Stream stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            this.Save(stream);
        }

        /// <summary>
        /// Saves this model to a stream.
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        public void Save(Stream stream)
        {
            Serializer.WriteObject(stream, this);
        }

        private static DataContractSerializer CreateSerializer()
        {
            Type[] knownTypes =
            {
                typeof(DisplayAttribute),
                typeof(Connector),
                typeof(LineBoundary),
                typeof(BorderBoundary),
                typeof(StencilEllipse),
                typeof(StencilParallelLines),
                typeof(StencilRectangle),
                typeof(string[]),
            };

            return new DataContractSerializer(typeof(ThreatModel), knownTypes);
        }
    }
}
