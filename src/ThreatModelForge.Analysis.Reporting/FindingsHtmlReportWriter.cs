namespace ThreatModelForge.Analysis.Reporting
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Xml;
    using ThreatModelForge.Analysis;

    /// <summary>
    /// Writes threat-model analysis findings (a <see cref="ModelReport"/>) as a self-contained
    /// HTML document. Used by <c>tmforge analyze --reportFolder</c>.
    /// </summary>
    public class FindingsHtmlReportWriter : ReportWriter
    {
        private const string CssResourceName = "ThreatModelForge.Analysis.Reporting.Report.css";

        private const string TableBordededStyle = "table-bordered";

        /// <summary>
        /// Initializes a new instance of the <see cref="FindingsHtmlReportWriter"/> class.
        /// </summary>
        /// <param name="path">The path to the output file.</param>
        public FindingsHtmlReportWriter(string path)
            : this(CreateXmlWriter(path), path)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FindingsHtmlReportWriter"/> class.
        /// </summary>
        /// <param name="inner">The inner xml writer.</param>
        /// <param name="path">The output path.</param>
        public FindingsHtmlReportWriter(XmlWriter inner, string path)
        {
            this.Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.Path = !string.IsNullOrWhiteSpace(path) ?
                path :
                throw new ArgumentOutOfRangeException(nameof(path));
        }

        /// <summary>
        /// Gets the inner writer.
        /// </summary>
        public XmlWriter Inner { get; }

        /// <summary>
        /// Gets the path to the output.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Writes the report.
        /// </summary>
        /// <param name="report">The report to write.</param>
        public override void Write(ModelReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            this.Inner.WriteStartDocument();
            this.Inner.WriteStartElement("html");
            this.Inner.WriteStartElement("head");
            this.Inner.WriteElementString("title", report.ThreatModelName);
            this.Inner.WriteStartElement("style");
            this.Inner.WriteAttributeString("type", "text/css");
            this.Inner.WriteString(LoadCss());
            this.Inner.WriteEndElement(); // style
            this.Inner.WriteEndElement(); // head

            this.Inner.WriteStartElement("body");
            this.Inner.WriteElementString("h1", $"Threat Model Forge Analysis Report for {report.ThreatModelName}");
            this.WriteSummaryBlock(report);
            this.WriteMessageBlock(report);

            foreach (DiagramSummary diagram in report.DiagramSummaries)
            {
                this.WriteDiagramSummary(report, diagram);
            }

            this.WriteRuleReportBlock(report);

            this.Inner.WriteEndElement(); // body

            this.Inner.WriteEndElement(); // html
            this.Inner.WriteEndDocument();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing && this.Inner != null)
            {
                this.Inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private static XmlWriter CreateXmlWriter(string path)
        {
            XmlWriterSettings writerSettings = new XmlWriterSettings()
            {
                Indent = true,
            };

            return XmlWriter.Create(path, writerSettings);
        }

        private static IEnumerable<ExpandedMessage> GetMessages(ModelReport report)
        {
            return
                from r in report.RuleReports
                from m in r.Messages
                select new ExpandedMessage
                {
                    RuleID = r.ID,
                    HelpUri = r.HelpUri,
                    Severity = r.Severity,
                    Diagram = m.Diagram is Guid diagramId ?
                        report.DiagramSummaries.FirstOrDefault(e => e.ID == diagramId) :
                        null,
                    Entity = m.Entity,
                    Text = m.Text,
                };
        }

        private static string LoadCss()
        {
            using (System.IO.Stream res = System.Reflection.Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream(CssResourceName))
            {
                using (System.IO.StreamReader reader = new System.IO.StreamReader(res))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static string? SeverityToRowCssClass(MessageSeverity severity)
        {
            string? result = null;
            switch (severity)
            {
                case MessageSeverity.Error:
                    result = "table-danger";
                    break;
                case MessageSeverity.Warning:
                    result = "table-warning";
                    break;
                case MessageSeverity.Info:
                    result = "table-info";
                    break;
            }

            return result;
        }

        private void WriteKeyValuePairTable(
            IEnumerable<KeyValuePair<string, object>> values,
            string? cssClass = null)
        {
            this.Inner.WriteStartElement("table");
            if (!string.IsNullOrWhiteSpace(cssClass))
            {
                this.Inner.WriteAttributeString("class", cssClass);
            }

            this.Inner.WriteStartElement("tbody");
            foreach (var row in values)
            {
                this.Inner.WriteStartElement("tr");
                this.Inner.WriteElementString("th", row.Key);
                this.Inner.WriteStartElement("td");
                this.Transform(row.Value);
                this.Inner.WriteEndElement(); // td
                this.Inner.WriteEndElement(); // tr
            }

            this.Inner.WriteEndElement(); // tbody
            this.Inner.WriteEndElement(); // table
        }

        private void WriteTable(
            IEnumerable<string> headers,
            IEnumerable<UnstructuredRow> rows,
            string? cssClass = null)
        {
            this.Inner.WriteStartElement("table");
            if (!string.IsNullOrWhiteSpace(cssClass))
            {
                this.Inner.WriteAttributeString("class", cssClass);
            }

            this.Inner.WriteStartElement("thead");
            this.Inner.WriteStartElement("tr");
            foreach (string header in headers)
            {
                this.Inner.WriteElementString("th", header);
            }

            this.Inner.WriteEndElement(); // tr
            this.Inner.WriteEndElement(); // thead

            this.Inner.WriteStartElement("tbody");
            foreach (var row in rows)
            {
                this.Inner.WriteStartElement("tr");
                if (!string.IsNullOrWhiteSpace(row.CssClass))
                {
                    this.Inner.WriteAttributeString("class", row.CssClass);
                }

                foreach (object cell in row.Data ?? Array.Empty<object>())
                {
                    this.Inner.WriteStartElement("td");
                    this.Transform(cell);
                    this.Inner.WriteEndElement();
                }

                this.Inner.WriteEndElement(); // tr
            }

            this.Inner.WriteEndElement(); // tbody

            this.Inner.WriteEndElement(); // table
        }

        private void WriteRuleReportBlock(ModelReport report)
        {
            this.StartCard();
            this.Inner.WriteElementString("h2", "Rule Configuration");
            List<RuleReport> sortedRules = new List<RuleReport>();
            sortedRules.AddRange(report.RuleReports);
            sortedRules.Sort((x, y) => string.CompareOrdinal(x.ID, y.ID));
            var rows =
                from e in sortedRules
                select new UnstructuredRow
                {
                    Data = new object[]
                    {
                        new RuleIdLinkTransformer(e.ID ?? string.Empty, e.HelpUri),
                        e.Disabled ? "Disabled" : e.Severity.ToString(),
                        e.Messages.Count.ToString(CultureInfo.InvariantCulture),
                        e.SuppressedMessages.Count.ToString(CultureInfo.InvariantCulture),
                        e.AnalyzerId ?? string.Empty,
                    },
                };
            this.WriteTable(
                new string[] { "Rule ID", "Severity", "Messages", "Suppressed Messages", "Analyzer" },
                rows,
                TableBordededStyle);
            this.EndCard();
        }

        private void WriteMessageBlock(ModelReport report)
        {
            this.StartCard();
            this.Inner.WriteElementString("h2", "Messages");
            var rowData = GetMessages(report);
            var sortedRows =
                rowData
                .OrderBy(m => (int)m.Severity)
                .ThenBy(m => m.RuleID);
            var rows =
                from r in sortedRows
                select new UnstructuredRow
                {
                    CssClass = SeverityToRowCssClass(r.Severity),
                    Data = new object[]
                    {
                        new RuleIdLinkTransformer(r.RuleID ?? string.Empty, r.HelpUri),
                        r.Severity.ToString(),
                        r.Diagram?.Header ?? string.Empty,
                        r.Entity ?? string.Empty,
                        r.Text ?? string.Empty,
                    },
                };
            this.WriteTable(
                new string[] { "Rule ID", "Severity", "Diagram", "Entity", "Description" },
                rows,
                TableBordededStyle);
            this.EndCard();
        }

        private void WriteDiagramSummary(ModelReport report, DiagramSummary diagram)
        {
            this.StartCard();
            this.Inner.WriteElementString("h2", $"Diagram: {diagram.Header}");
            KeyValuePair<string, object>[] rows = new KeyValuePair<string, object>[]
            {
                new KeyValuePair<string, object>("Components", diagram.ComponentCount.ToString(CultureInfo.InvariantCulture)),
                new KeyValuePair<string, object>("Connectors", diagram.ConnectorCount.ToString(CultureInfo.InvariantCulture)),
                new KeyValuePair<string, object>("Trust Boundaries", diagram.TrustBoundaryCount.ToString(CultureInfo.InvariantCulture)),
            };

            this.WriteKeyValuePairTable(rows);

            var messageRowData =
                from m in GetMessages(report)
                where object.ReferenceEquals(m.Diagram, diagram)
                select m;
            var sortedMessageRows =
                messageRowData
                .OrderBy(m => (int)m.Severity)
                .ThenBy(m => m.RuleID);
            var messageRows =
                from r in sortedMessageRows
                select new UnstructuredRow
                {
                    CssClass = SeverityToRowCssClass(r.Severity),
                    Data = new object[]
                    {
                        new RuleIdLinkTransformer(r.RuleID ?? string.Empty, r.HelpUri),
                        r.Severity.ToString(),
                        r.Entity ?? string.Empty,
                        r.Text ?? string.Empty,
                    },
                };

            this.Inner.WriteElementString("h4", "Messages");
            this.WriteTable(
                new string[] { "Rule ID", "Severity", "Entity", "Description" },
                messageRows,
                TableBordededStyle);

            this.EndCard();
        }

        private void WriteSummaryBlock(ModelReport report)
        {
            this.StartCard();
            this.Inner.WriteElementString("h2", "Summary");
            KeyValuePair<string, object>[] rows = new KeyValuePair<string, object>[]
            {
                new KeyValuePair<string, object>("Source Path", report.SourcePath ?? string.Empty),
                new KeyValuePair<string, object>("Owner", report.Owner ?? string.Empty),
                new KeyValuePair<string, object>("Reviewer", report.Reviewer ?? string.Empty),
                new KeyValuePair<string, object>("Contributors", report.Contributors ?? string.Empty),
                new KeyValuePair<string, object>("Threat Generation Enabled", report.ThreatGenerationEnabled.ToString()),
                new KeyValuePair<string, object>("Assumptions", report.Assumptions ?? string.Empty),
                new KeyValuePair<string, object>("Description", report.Description ?? string.Empty),
                new KeyValuePair<string, object>("External Dependencies", report.ExternalDependencies ?? string.Empty),
                new KeyValuePair<string, object>("Total Diagrams", report.DiagramSummaries.Count.ToString(CultureInfo.InvariantCulture)),
                new KeyValuePair<string, object>("Total Components", report.DiagramSummaries.Select(e => e.ComponentCount).Sum().ToString(CultureInfo.InvariantCulture)),
                new KeyValuePair<string, object>("Total Connections", report.DiagramSummaries.Select(e => e.ConnectorCount).Sum().ToString(CultureInfo.InvariantCulture)),
                new KeyValuePair<string, object>("Total Trust Boundaries", report.DiagramSummaries.Select(e => e.TrustBoundaryCount).Sum().ToString(CultureInfo.InvariantCulture)),
                new KeyValuePair<string, object>("Microsoft Threat Modeling Tool Version", report.ToolVersion ?? string.Empty),
                new KeyValuePair<string, object>("Knowledge Base (Template) Name", report.KnowledgeBaseName ?? string.Empty),
                new KeyValuePair<string, object>("Knowledge Base (Template) Version", report.KnowledgeBaseVersion ?? string.Empty),
            };

            this.WriteKeyValuePairTable(rows);
            this.Inner.WriteElementString("h3", "Custom Properties");
            List<KeyValuePair<string, object>> customPropertyRows = new List<KeyValuePair<string, object>>();
            foreach (KeyValuePair<string, string> prop in report.CustomProperties)
            {
                customPropertyRows.Add(new KeyValuePair<string, object>(prop.Key, prop.Value));
            }

            this.WriteKeyValuePairTable(customPropertyRows);
            this.EndCard();
        }

        /// <summary>
        /// Starts a div element with the card class.
        /// </summary>
        private void StartCard(string cssClass = "card")
        {
            this.Inner.WriteStartElement("div");
            this.Inner.WriteAttributeString("class", cssClass);
        }

        /// <summary>
        /// Ends a div element started with <see cref="StartCard(string)"/>.
        /// </summary>
        private void EndCard()
        {
            this.Inner.WriteEndElement(); // div
        }

        private void Transform(object value)
        {
            if (!(value is Transformer t))
            {
                t = new TextTransformer(value?.ToString() ?? string.Empty);
            }

            t.Write(this.Inner);
        }

        /// <summary>
        /// Allows custom formatting of child elements.
        /// </summary>
        private abstract class Transformer
        {
            /// <summary>
            /// Writes the transformed value to the writer.
            /// </summary>
            /// <param name="writer">The writer to receive the output.</param>
            public abstract void Write(XmlWriter writer);
        }

        /// <summary>
        /// Transforms a string to text output.
        /// </summary>
        private class TextTransformer : Transformer
        {
            public TextTransformer(string text)
            {
                this.Text = text ?? string.Empty;
            }

            public string Text { get; }

            public override void Write(XmlWriter writer)
            {
                writer.WriteString(this.Text);
            }
        }

        /// <summary>
        /// Transforms a rule id into a link to the wiki.
        /// </summary>
        private class RuleIdLinkTransformer : Transformer
        {
            public RuleIdLinkTransformer(string ruleID, Uri? helpUri)
            {
                this.RuleID = ruleID;
                this.HelpUri = helpUri;
            }

            public string RuleID { get; }

            public Uri? HelpUri { get; }

            public override void Write(XmlWriter writer)
            {
                if (this.HelpUri == null)
                {
                    writer.WriteString(this.RuleID);
                    return;
                }

                writer.WriteStartElement("a");
                writer.WriteAttributeString("href", this.HelpUri.ToString());
                writer.WriteString(this.RuleID);
                writer.WriteEndElement();
            }
        }

        private class ExpandedMessage
        {
            public string? RuleID { get; set; }

            public Uri? HelpUri { get; set; }

            public MessageSeverity Severity { get; set; }

            public DiagramSummary? Diagram { get; set; }

            public string? Entity { get; set; }

            public string? Text { get; set; }
        }

        private class UnstructuredRow
        {
            public string? CssClass { get; set; }

            public IEnumerable<object>? Data { get; set; }
        }
    }
}
