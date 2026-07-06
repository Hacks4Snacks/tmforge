namespace ThreatModelForge.Reporting.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Reporting;

    /// <summary>
    /// Unit tests for <see cref="HtmlReportWriter"/>.
    /// </summary>
    [TestClass]
    public class HtmlReportWriterTest
    {
        /// <summary>
        /// Verifies the report contains the metadata, the diagram SVG, and the threats.
        /// </summary>
        [TestMethod]
        public void WritesReportWithMetadataDiagramAndThreats()
        {
            string html = new HtmlReportWriter().Write(BuildModel("Spoofing of the web app"));

            StringAssert.StartsWith(html, "<!DOCTYPE html>");
            StringAssert.Contains(html, "Threat Modeling Report");
            StringAssert.Contains(html, "Test Model");
            StringAssert.Contains(html, "Main DFD");
            StringAssert.Contains(html, "Spoofing of the web app");
            StringAssert.Contains(html, "Needs Investigation");
            StringAssert.Contains(html, "<svg");
        }

        /// <summary>
        /// Verifies that user-supplied threat text is HTML-escaped, so a report cannot inject markup.
        /// </summary>
        [TestMethod]
        public void EscapesUserSuppliedText()
        {
            string html = new HtmlReportWriter().Write(BuildModel("<script>alert(1)</script>"));

            Assert.IsFalse(html.Contains("<script>", StringComparison.Ordinal), "user text must be HTML-escaped");
            StringAssert.Contains(html, "&lt;script&gt;");
        }

        private static ThreatModel BuildModel(string threatTitle)
        {
            StencilRectangle rectangle = new StencilRectangle { Guid = Guid.NewGuid(), TypeId = "GE.P", Left = 0, Top = 0, Width = 80, Height = 40 };
            Connector flow = new Connector { Guid = Guid.NewGuid(), TypeId = "GE.DF", SourceX = 80, SourceY = 20, TargetX = 200, TargetY = 30 };
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "Main DFD" };
            diagram.Borders[rectangle.Guid] = rectangle;
            diagram.Lines[flow.Guid] = flow;

            ThreatModel model = new ThreatModel
            {
                MetaInformation = new MetaInformation { ThreatModelName = "Test Model", Owner = "graymark" },
            };
            model.DrawingSurfaceList.Add(diagram);

            Threat threat = new Threat
            {
                TypeId = "T1",
                Title = threatTitle,
                DrawingSurfaceGuid = diagram.Guid,
                State = ThreatState.NeedsInvestigation,
                Priority = "High",
                UserThreatDescription = "A spoofing threat.",
                StateInformation = "Enforce authentication.",
            };
            model.AllThreatsDictionary[Guid.NewGuid().ToString("N")] = threat;
            return model;
        }
    }
}
