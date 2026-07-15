namespace ThreatModelForge.Analysis.Reporting.Tests
{
    using System;
    using System.IO;
    using System.Text;
    using System.Xml;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Security and rendering tests for <see cref="FindingsHtmlReportWriter"/>.
    /// </summary>
    [TestClass]
    public class FindingsHtmlReportWriterTests
    {
        /// <summary>Only HTTP(S) rule help URIs are emitted as clickable links.</summary>
        [TestMethod]
        public void EmitsOnlySafeRuleHelpLinks()
        {
            ModelReport report = new ModelReport { ThreatModelName = "Test" };
            report.RuleReports.Add(new RuleReport
            {
                ID = "UNSAFE",
                HelpUri = new Uri("javascript:alert(1)"),
                Severity = MessageSeverity.Warning,
            });
            report.RuleReports.Add(new RuleReport
            {
                ID = "SAFE",
                HelpUri = new Uri("https://example.test/help"),
                Severity = MessageSeverity.Warning,
            });
            report.RuleReports.Add(new RuleReport
            {
                ID = "RELATIVE",
                HelpUri = new Uri("help/rule", UriKind.Relative),
                Severity = MessageSeverity.Warning,
            });

            StringBuilder output = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings { OmitXmlDeclaration = true };
            using (XmlWriter inner = XmlWriter.Create(new StringWriter(output), settings))
            using (FindingsHtmlReportWriter writer = new FindingsHtmlReportWriter(inner, "report.html"))
            {
                writer.Write(report);
            }

            string html = output.ToString();
            Assert.IsFalse(html.Contains("href=\"javascript:", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(html.Contains("href=\"help/rule", StringComparison.OrdinalIgnoreCase));
            StringAssert.Contains(html, "UNSAFE");
            StringAssert.Contains(html, "RELATIVE");
            StringAssert.Contains(html, "href=\"https://example.test/help\"");
        }

        /// <summary>The findings report renders generalized category identity and priority.</summary>
        [TestMethod]
        public void EmitsGeneralizedThreatMetadata()
        {
            ModelReport report = new ModelReport { ThreatModelName = "Test" };
            report.ThreatCategories.Add(new RuleThreatCategory(
                "medical/privacy",
                "privacy",
                "Privacy",
                "Patient privacy",
                "Privacy harm."));
            report.RuleReports.Add(new RuleReport
            {
                ID = "PRIV-1",
                Severity = MessageSeverity.Info,
                ThreatCategoryId = "medical/privacy",
                ThreatCategoryName = "Privacy",
                DefaultThreatPriority = ThreatPriority.High,
            });
            StringBuilder output = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings { OmitXmlDeclaration = true };
            using (XmlWriter inner = XmlWriter.Create(new StringWriter(output), settings))
            using (FindingsHtmlReportWriter writer = new FindingsHtmlReportWriter(inner, "report.html"))
            {
                writer.Write(report);
            }

            string html = output.ToString();
            StringAssert.Contains(html, "Threat Categories");
            StringAssert.Contains(html, "medical/privacy");
            StringAssert.Contains(html, "Patient privacy");
            StringAssert.Contains(html, "Privacy harm.");
            StringAssert.Contains(html, "Threat Category");
            StringAssert.Contains(html, "Default Threat Priority");
            StringAssert.Contains(html, ">Privacy<");
            StringAssert.Contains(html, ">High<");
        }
    }
}
