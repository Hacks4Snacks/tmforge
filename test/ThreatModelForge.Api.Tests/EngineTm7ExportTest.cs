namespace ThreatModelForge.Api.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Engine;

    /// <summary>
    /// Regression tests for the user-facing <c>.tm7</c> generation paths (<see cref="EngineService.ExportTm7"/>
    /// and <see cref="EngineService.Convert"/>), asserting the serialized document carries what the
    /// Microsoft Threat Modeling Tool (MTMT) needs to open it: the recognized version stamp, the
    /// document scaffolding, non-nil connector ports, and — for the lossless export path — an embedded
    /// knowledge base. These exercise the real engine pipeline the CLI, API, and WebAssembly hosts use.
    /// </summary>
    [TestClass]
    public class EngineTm7ExportTest
    {
        private const string XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";

        /// <summary>
        /// The lossless export embeds the knowledge base so MTMT resolves threat types and typed
        /// properties when it opens the file.
        /// </summary>
        [TestMethod]
        public void ExportTm7EmbedsKnowledgeBase()
        {
            AssertEmbedsKnowledgeBase(Parse(EngineService.ExportTm7(ConnectedModel())));
        }

        /// <summary>
        /// The generic <c>convert --to tm7</c> path embeds the knowledge base too, so every path that
        /// writes a <c>.tm7</c> (including the Studio "Download .tm7" and the API convert endpoint, which
        /// both route through this method) produces the same tool-openable file.
        /// </summary>
        [TestMethod]
        public void ConvertToTm7EmbedsKnowledgeBase()
        {
            AssertEmbedsKnowledgeBase(Parse(EngineService.Convert(ConnectedModel(), "tm7")));
        }

        /// <summary>
        /// The exported document carries the scaffolding MTMT reads without a null guard: the recognized
        /// version, the metadata block, the profile's prompted knowledge-base versions, drawing-surface
        /// type identifiers, and non-nil connector ports.
        /// </summary>
        [TestMethod]
        public void ExportTm7CarriesToolScaffolding()
        {
            XDocument doc = Parse(EngineService.ExportTm7(ConnectedModel()));

            AssertOpenableScaffolding(doc);
        }

        /// <summary>
        /// The exported bytes read back through the engine without loss.
        /// </summary>
        [TestMethod]
        public void ExportTm7OutputReloads()
        {
            byte[] bytes = EngineService.ExportTm7(ConnectedModel());

            TmForgeModelDto restored = EngineService.ReadModel(bytes, "tm7");

            Assert.IsNotNull(restored.Elements);
            CollectionAssert.AreEquivalent(
                new[] { "Web App", "Database" },
                restored.Elements!.Select(e => e.Name).ToArray());
        }

        /// <summary>
        /// The generic <c>convert --to tm7</c> path also stamps the document scaffolding MTMT reads
        /// without a null guard, matching <see cref="EngineService.ExportTm7"/>.
        /// </summary>
        [TestMethod]
        public void ConvertToTm7CarriesToolScaffolding()
        {
            XDocument doc = Parse(EngineService.Convert(ConnectedModel(), "tm7"));

            AssertOpenableScaffolding(doc);
        }

        /// <summary>
        /// MTMT rejects drawing coordinates below a small minimum and silently corrects the file on
        /// open. This reproduces a Studio export whose elements sit at or above the canvas origin
        /// (Top &lt;= 0) and asserts the exported document shifts every element and connector coordinate
        /// to or beyond that minimum, so the tool opens it without a correction warning.
        /// </summary>
        [TestMethod]
        public void ConvertToTm7NormalizesCoordinatesBelowTheToolMinimum()
        {
            TmForgeModelDto dto = new TmForgeModelDto
            {
                Schema = "tmforge-json",
                Version = "0.1",
                Elements = new[]
                {
                    new TmForgeElementDto { Id = "p", Kind = "process", Name = "Process", X = 400, Y = -16, Width = 100, Height = 60 },
                    new TmForgeElementDto { Id = "h", Kind = "external", Name = "Human Actor", X = 80, Y = 0, Width = 120, Height = 60 },
                },
                Flows = new[]
                {
                    new TmForgeFlowDto { Id = "f", Source = "h", Target = "p", Name = "uses" },
                },
            };

            XDocument doc = Parse(EngineService.Convert(dto, "tm7"));

            List<int> coordinates = CoordinateValues(doc).ToList();
            Assert.IsTrue(coordinates.Count > 0, "Expected the surface to carry coordinates.");
            Assert.IsTrue(
                coordinates.All(v => v >= 10),
                "Every border and connector coordinate must sit at or beyond the tool minimum (10); found " + coordinates.Min() + ".");
            Assert.AreEqual(
                10,
                coordinates.Min(),
                "The whole-surface shift should land the lowest coordinate exactly on the minimum.");
        }

        private static void AssertOpenableScaffolding(XDocument doc)
        {
            XElement? version = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Version");
            Assert.IsNotNull(version, "Expected a <Version> element.");
            Assert.AreEqual("4.3", version!.Value);

            Assert.IsTrue(
                doc.Descendants().Any(e => e.Name.LocalName == "MetaInformation"),
                "Expected a <MetaInformation> block.");
            Assert.IsTrue(
                doc.Descendants().Any(e => e.Name.LocalName == "PromptedKb"),
                "Expected the profile's <PromptedKb> block.");

            XElement surface = doc.Descendants().First(e => e.Name.LocalName == "DrawingSurfaceModel");
            Assert.AreEqual(
                "DRAWINGSURFACE",
                surface.Elements().First(e => e.Name.LocalName == "GenericTypeId").Value);
            Assert.AreEqual(
                "DRAWINGSURFACE",
                surface.Elements().First(e => e.Name.LocalName == "TypeId").Value);

            var ports = doc.Descendants()
                .Where(e => e.Name.LocalName == "PortSource" || e.Name.LocalName == "PortTarget")
                .ToList();
            Assert.IsTrue(ports.Count >= 2, "Expected the connector to serialize both ports.");
            Assert.IsTrue(
                ports.All(p => p.Attribute(XName.Get("nil", XsiNamespace)) == null && p.Value == "None"),
                "Connector ports must serialize as \"None\", never nil.");
        }

        private static void AssertEmbedsKnowledgeBase(XDocument doc)
        {
            XElement? kb = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "KnowledgeBase");
            Assert.IsNotNull(kb, "The .tm7 must contain a <KnowledgeBase> element.");
            Assert.IsNull(
                kb!.Attribute(XName.Get("nil", XsiNamespace)),
                "The knowledge base must be embedded, not nil.");
            Assert.IsTrue(kb.HasElements, "The embedded knowledge base must carry content.");
        }

        private static IEnumerable<int> CoordinateValues(XDocument doc)
        {
            string[] names = { "Left", "Top", "SourceX", "SourceY", "TargetX", "TargetY", "HandleX", "HandleY" };
            foreach (XElement surface in doc.Descendants().Where(e => e.Name.LocalName == "DrawingSurfaceModel"))
            {
                foreach (XElement coordinate in surface.Descendants().Where(e => names.Contains(e.Name.LocalName)))
                {
                    bool parsed = int.TryParse(coordinate.Value, out int value);
                    if (parsed)
                    {
                        yield return value;
                    }
                }
            }
        }

        private static XDocument Parse(byte[] bytes)
        {
            using MemoryStream stream = new MemoryStream(bytes);
            return XDocument.Load(stream);
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
                    new TmForgeFlowDto { Id = "f1", Source = "web", Target = "db", Name = "writes" },
                },
            };
        }
    }
}
