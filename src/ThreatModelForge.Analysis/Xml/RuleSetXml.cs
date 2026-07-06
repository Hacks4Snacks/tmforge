namespace ThreatModelForge.Analysis.Xml
{
    using System;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Xml;
    using System.Xml.Serialization;

    /// <summary>
    /// Deserialized ruleset document.
    /// </summary>
    /// <remarks>
    /// The format is a subset of the ruleset files used by FxCop.
    /// https://docs.microsoft.com/en-us/visualstudio/code-quality/using-rule-sets-to-group-code-analysis-rules?view=vs-2019.
    /// </remarks>
    [XmlRoot(ElementName = "RuleSet")]
    public class RuleSetXml
    {
        /// <summary>
        /// Gets or sets the friendly name of the rule set.
        /// </summary>
        [XmlAttribute(AttributeName = "Name")]
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        [XmlAttribute(AttributeName = "Description")]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the tools version.
        /// </summary>
        [XmlAttribute(AttributeName = "ToolsVersion")]
        public string? ToolsVersion { get; set; }

        /// <summary>
        /// Gets the rules declarations.
        /// </summary>
        [XmlElement(ElementName = "Rules")]
        public Collection<RulesXml> Rules { get; } = new Collection<RulesXml>();

        /// <summary>
        /// Loads a <see cref="RuleSetXml"/> document.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>A new instance of the <see cref="RuleSetXml"/> class.</returns>
        public static RuleSetXml Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentOutOfRangeException(nameof(path));
            }

            XmlSerializer serializer = new XmlSerializer(typeof(RuleSetXml));
            using (XmlReader reader = XmlReader.Create(new FileStream(path, FileMode.Open, FileAccess.Read)))
            {
                return (RuleSetXml)serializer.Deserialize(reader);
            }
        }
    }
}
