namespace ThreatModelForge.Analysis.Xml
{
    using System.Xml.Serialization;

    /// <summary>
    /// The action for the rule in the file.
    /// </summary>
    public enum RuleAction
    {
        /// <summary>
        /// Rule is suppressed.
        /// </summary>
        None = 0,

        /// <summary>
        /// Rule messages should be logged as info.
        /// </summary>
        Info,

        /// <summary>
        /// Rule messages should be logged as warnings.
        /// </summary>
        Warning,

        /// <summary>
        /// Rule messages should be logged as errors.
        /// </summary>
        Error,
    }

    /// <summary>
    /// Individual rule override.
    /// </summary>
    public class RuleXml
    {
        /// <summary>
        /// Gets or sets the rule id.
        /// </summary>
        [XmlAttribute("Id")]
        public string? Id { get; set; }

        /// <summary>
        /// Gets or sets the action to take for the rule.
        /// </summary>
        [XmlAttribute("Action")]
        public RuleAction Action { get; set; }
    }
}
