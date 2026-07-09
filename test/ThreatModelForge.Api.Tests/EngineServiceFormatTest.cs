namespace ThreatModelForge.Api.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Engine;

    /// <summary>
    /// Unit tests for the format-facing methods of <see cref="EngineService"/> —
    /// <see cref="EngineService.ReadModel"/>, <see cref="EngineService.Convert"/>,
    /// <see cref="EngineService.Detect"/>, and <see cref="EngineService.Report"/> — which are the
    /// single seam the CLI, API, and WebAssembly hosts all funnel document I/O through.
    /// </summary>
    [TestClass]
    public class EngineServiceFormatTest
    {
        /// <summary>
        /// Converting to the canonical tmforge-json format and reading it back preserves the model's
        /// elements and flows (name, kind, and flow properties) through the facade.
        /// </summary>
        [TestMethod]
        public void ConvertToTmForgeJsonRoundTripsThroughReadModel()
        {
            byte[] bytes = EngineService.Convert(ConnectedModel(), "tmforge-json");

            TmForgeModelDto restored = EngineService.ReadModel(bytes, "tmforge-json");

            Assert.IsNotNull(restored.Elements);
            Assert.IsNotNull(restored.Flows);
            CollectionAssert.AreEquivalent(
                new[] { "Web App", "Database" },
                restored.Elements!.Select(e => e.Name).ToArray());
            Assert.AreEqual(1, restored.Flows!.Count);
            Assert.AreEqual("writes", restored.Flows[0].Name);
            Assert.AreEqual("TLS", restored.Flows[0].Properties["Protocol"]);
        }

        /// <summary>
        /// Converting to the lossless <c>.tm7</c> format and reading it back preserves the model's
        /// elements and flows through the facade.
        /// </summary>
        [TestMethod]
        public void ConvertToTm7RoundTripsThroughReadModel()
        {
            byte[] bytes = EngineService.Convert(ConnectedModel(), "tm7");

            TmForgeModelDto restored = EngineService.ReadModel(bytes, "tm7");

            Assert.IsNotNull(restored.Elements);
            Assert.IsNotNull(restored.Flows);
            CollectionAssert.AreEquivalent(
                new[] { "Web App", "Database" },
                restored.Elements!.Select(e => e.Name).ToArray());
            Assert.AreEqual(1, restored.Flows!.Count);
            Assert.AreEqual("writes", restored.Flows[0].Name);
        }

        /// <summary>
        /// When no format id is supplied, <see cref="EngineService.ReadModel"/> sniffs the content and
        /// still parses it.
        /// </summary>
        [TestMethod]
        public void ReadModelSniffsFormatWhenIdOmitted()
        {
            byte[] bytes = EngineService.Convert(SingleProcessModel(), "tm7");

            TmForgeModelDto restored = EngineService.ReadModel(bytes, null);

            Assert.IsNotNull(restored.Elements);
            Assert.AreEqual(1, restored.Elements!.Count);
            Assert.AreEqual("Web App", restored.Elements[0].Name);
        }

        /// <summary>
        /// <see cref="EngineService.ReadModel"/> rejects null content.
        /// </summary>
        [TestMethod]
        public void ReadModelNullContentThrows()
        {
            Assert.Throws<ArgumentNullException>(() => EngineService.ReadModel(null!, "tm7"));
        }

        /// <summary>
        /// <see cref="EngineService.Convert"/> rejects an empty or null format id.
        /// </summary>
        [TestMethod]
        public void ConvertEmptyFormatIdThrows()
        {
            Assert.Throws<ArgumentException>(() => EngineService.Convert(SingleProcessModel(), string.Empty));
            Assert.Throws<ArgumentException>(() => EngineService.Convert(SingleProcessModel(), null!));
        }

        /// <summary>
        /// <see cref="EngineService.Convert"/> rejects a format id that is not registered.
        /// </summary>
        [TestMethod]
        public void ConvertUnknownFormatIdThrows()
        {
            Assert.Throws<NotSupportedException>(
                () => EngineService.Convert(SingleProcessModel(), "does-not-exist"));
        }

        /// <summary>
        /// <see cref="EngineService.Detect"/> recognizes tmforge-json content by sniffing.
        /// </summary>
        [TestMethod]
        public void DetectRecognizesTmForgeJson()
        {
            byte[] bytes = EngineService.Convert(SingleProcessModel(), "tmforge-json");

            FormatDto? detected = EngineService.Detect(bytes);

            Assert.IsNotNull(detected);
            Assert.AreEqual("tmforge-json", detected!.Id);
        }

        /// <summary>
        /// <see cref="EngineService.Detect"/> recognizes <c>.tm7</c> content by sniffing.
        /// </summary>
        [TestMethod]
        public void DetectRecognizesTm7()
        {
            byte[] bytes = EngineService.Convert(SingleProcessModel(), "tm7");

            FormatDto? detected = EngineService.Detect(bytes);

            Assert.IsNotNull(detected);
            Assert.AreEqual("tm7", detected!.Id);
        }

        /// <summary>
        /// <see cref="EngineService.Detect"/> returns null when the content matches no known format.
        /// </summary>
        [TestMethod]
        public void DetectUnrecognizedContentReturnsNull()
        {
            byte[] bytes = Encoding.UTF8.GetBytes("this is plainly not a threat model document");

            Assert.IsNull(EngineService.Detect(bytes));
        }

        /// <summary>
        /// <see cref="EngineService.Detect"/> rejects null content.
        /// </summary>
        [TestMethod]
        public void DetectNullContentThrows()
        {
            Assert.Throws<ArgumentNullException>(() => EngineService.Detect(null!));
        }

        /// <summary>
        /// <see cref="EngineService.Report"/> renders an HTML document for the default format.
        /// </summary>
        [TestMethod]
        public void ReportHtmlProducesHtmlDocument()
        {
            byte[] bytes = EngineService.Report(SingleProcessModel(), "html");

            string report = Encoding.UTF8.GetString(bytes);
            Assert.IsTrue(report.Contains('<'), "Expected markup in the HTML report.");
            Assert.IsTrue(
                report.IndexOf("html", StringComparison.OrdinalIgnoreCase) >= 0,
                "Expected an HTML report to contain an html tag.");
        }

        /// <summary>
        /// <see cref="EngineService.Report"/> renders SVG when asked, matching the format string
        /// case-insensitively.
        /// </summary>
        [TestMethod]
        public void ReportSvgIsCaseInsensitive()
        {
            byte[] bytes = EngineService.Report(SingleProcessModel(), "SVG");

            string report = Encoding.UTF8.GetString(bytes);
            Assert.IsTrue(
                report.IndexOf("<svg", StringComparison.OrdinalIgnoreCase) >= 0,
                "Expected an <svg> root for the SVG report.");
        }

        /// <summary>
        /// <see cref="EngineService.Report"/> falls back to HTML for an unrecognized report format.
        /// </summary>
        [TestMethod]
        public void ReportUnknownFormatFallsBackToHtml()
        {
            byte[] bytes = EngineService.Report(SingleProcessModel(), "pdf");

            string report = Encoding.UTF8.GetString(bytes);
            Assert.IsTrue(
                report.IndexOf("html", StringComparison.OrdinalIgnoreCase) >= 0,
                "An unknown report format should fall back to HTML.");
            Assert.IsFalse(
                report.TrimStart().StartsWith("<svg", StringComparison.OrdinalIgnoreCase),
                "The fallback should not be SVG.");
        }

        private static TmForgeModelDto SingleProcessModel()
        {
            return new TmForgeModelDto
            {
                Schema = "tmforge-json",
                Version = "0.1",
                Elements = new[]
                {
                    new TmForgeElementDto { Id = "web", Kind = "process", Name = "Web App", X = 100, Y = 100 },
                },
            };
        }

        private static TmForgeModelDto ConnectedModel()
        {
            return new TmForgeModelDto
            {
                Schema = "tmforge-json",
                Version = "0.1",
                Elements = new[]
                {
                    new TmForgeElementDto { Id = "web", Kind = "process", Name = "Web App", X = 100, Y = 100 },
                    new TmForgeElementDto { Id = "db", Kind = "datastore", Name = "Database", X = 300, Y = 100 },
                },
                Flows = new[]
                {
                    new TmForgeFlowDto
                    {
                        Id = "f1",
                        Source = "web",
                        Target = "db",
                        Name = "writes",
                        Properties = new Dictionary<string, string> { { "Protocol", "TLS" } },
                    },
                },
            };
        }
    }
}
