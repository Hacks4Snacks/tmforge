namespace ThreatModelForge.Model
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Per-model profile data. Persisted under the empty ("") data-contract namespace to match the
    /// on-disk schema.
    /// </summary>
    [DataContract(Name = "Profile", Namespace = "")]
    public class ProfileData
    {
        /// <summary>
        /// Gets or sets the knowledge-base versions the user has already been prompted about.
        /// </summary>
        [DataMember(EmitDefaultValue = true, Name = "PromptedKb", Order = 1)]
        public KbVersions? PromptedKb { get; set; }
    }
}
