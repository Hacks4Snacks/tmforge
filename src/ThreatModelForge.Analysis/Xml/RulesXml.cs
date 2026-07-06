namespace ThreatModelForge.Analysis.Xml
{
    using System.Collections.ObjectModel;
    using System.Xml.Serialization;

    /// <summary>
    /// A collection of rule overrides.
    /// </summary>
    public class RulesXml
    {
        /// <summary>
        /// Gets or sets the analyzer id (DLL name w/o extension).
        /// </summary>
        [XmlAttribute("AnalyzerId")]
        public string? AnalyzerId { get; set; }

        /// <summary>
        /// Gets or sets the rule namespace.
        /// </summary>
        [XmlAttribute("RuleNamespace")]
        public string? RuleNamespace { get; set; }

        /// <summary>
        /// Gets the rules.
        /// </summary>
        [XmlElement("Rule")]
        public Collection<RuleXml> Rules { get; } = new Collection<RuleXml>();
    }
}
