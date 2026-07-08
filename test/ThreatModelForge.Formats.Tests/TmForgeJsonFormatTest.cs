namespace ThreatModelForge.Formats.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for <see cref="TmForgeJsonFormat"/>.
    /// </summary>
    [TestClass]
    public class TmForgeJsonFormatTest
    {
        private const string SampleJson =
            "{\"schema\":\"tmforge-json\",\"version\":\"0.1\"," +
            "\"elements\":[" +
            "{\"id\":\"p1\",\"kind\":\"process\",\"name\":\"Web App\",\"x\":100,\"y\":100}," +
            "{\"id\":\"ds1\",\"kind\":\"datastore\",\"name\":\"Database\",\"x\":420,\"y\":100}]," +
            "\"flows\":[{\"id\":\"f1\",\"source\":\"p1\",\"target\":\"ds1\",\"name\":\"query\"}]}";

        private const string MultiPageJson =
            "{\"schema\":\"tmforge-json\",\"version\":\"0.1\"," +
            "\"elements\":[],\"flows\":[]," +
            "\"diagrams\":[" +
            "{\"id\":\"d1\",\"name\":\"Context\",\"elements\":[" +
            "{\"id\":\"u1\",\"kind\":\"external\",\"name\":\"User\",\"x\":100,\"y\":100}," +
            "{\"id\":\"g1\",\"kind\":\"process\",\"name\":\"Gateway\",\"x\":400,\"y\":100}]," +
            "\"flows\":[{\"id\":\"f1\",\"source\":\"u1\",\"target\":\"g1\",\"name\":\"request\"}]}," +
            "{\"id\":\"d2\",\"name\":\"Payments\",\"elements\":[" +
            "{\"id\":\"ps1\",\"kind\":\"process\",\"name\":\"Payment Svc\",\"x\":100,\"y\":100}," +
            "{\"id\":\"l1\",\"kind\":\"datastore\",\"name\":\"Ledger\",\"x\":400,\"y\":100}]," +
            "\"flows\":[{\"id\":\"f2\",\"source\":\"ps1\",\"target\":\"l1\",\"name\":\"write\"}]}]}";

        /// <summary>
        /// Verifies the provider advertises the canonical identifier and read+write capabilities.
        /// </summary>
        [TestMethod]
        public void AdvertisesIdentityAndCapabilities()
        {
            TmForgeJsonFormat format = new TmForgeJsonFormat();

            Assert.AreEqual("tmforge-json", format.Id);
            Assert.IsTrue(format.Capabilities.CanRead);
            Assert.IsTrue(format.Capabilities.CanWrite);
        }

        /// <summary>
        /// Verifies that a GUID element id in the source document is preserved as the element's
        /// identity, so the structural diff and three-way merge can match elements across files.
        /// A non-GUID id keeps the generated identity (unchanged behavior).
        /// </summary>
        [TestMethod]
        public void ReadPreservesGuidElementIds()
        {
            System.Guid id = System.Guid.NewGuid();
            string json = "{\"schema\":\"tmforge-json\",\"version\":\"0.1\",\"elements\":[" +
                "{\"id\":\"" + id + "\",\"kind\":\"process\",\"name\":\"P\",\"x\":0,\"y\":0}]}";

            ThreatModel model;
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                model = new TmForgeJsonFormat().Read(stream);
            }

            Assert.IsTrue(model.DrawingSurfaceList[0].Borders.ContainsKey(id));
        }

        /// <summary>
        /// Verifies content sniffing matches a tmforge-json document and rejects a <c>.tm7</c>
        /// document, leaving the stream position unchanged.
        /// </summary>
        [TestMethod]
        public void CanReadSniffsSchemaToken()
        {
            TmForgeJsonFormat format = new TmForgeJsonFormat();

            using (MemoryStream json = new MemoryStream(Encoding.UTF8.GetBytes(SampleJson)))
            using (MemoryStream xml = new MemoryStream(
                Encoding.UTF8.GetBytes("<ThreatModel xmlns=\"http://schemas.datacontract.org/2004/07/ThreatModeling.Model\">")))
            {
                Assert.IsTrue(format.CanRead(json));
                Assert.AreEqual(0, json.Position);
                Assert.IsFalse(format.CanRead(xml));
            }
        }

        /// <summary>
        /// Verifies a document round-trips element and flow structure through the engine model:
        /// read into a <see cref="ThreatModel"/>, then write back to tmforge-json.
        /// </summary>
        [TestMethod]
        public void RoundTripsElementsAndFlows()
        {
            TmForgeJsonFormat format = new TmForgeJsonFormat();

            ThreatModel model;
            using (MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(SampleJson)))
            {
                model = format.Read(input);
            }

            string json;
            using (MemoryStream output = new MemoryStream())
            {
                format.Write(model, output);
                json = Encoding.UTF8.GetString(output.ToArray());
            }

            using (JsonDocument parsed = JsonDocument.Parse(json))
            {
                JsonElement root = parsed.RootElement;
                Assert.AreEqual("tmforge-json", root.GetProperty("schema").GetString());
                Assert.AreEqual(2, root.GetProperty("elements").GetArrayLength());
                Assert.AreEqual(1, root.GetProperty("flows").GetArrayLength());
            }

            StringAssert.Contains(json, "Web App");
            StringAssert.Contains(json, "Database");
            StringAssert.Contains(json, "query");
        }

        /// <summary>
        /// A multi-page document maps to one drawing surface per page on read, and writes back one
        /// <c>diagrams</c> entry per surface (with its name) instead of flattening every page into a
        /// single element/flow list. The top-level arrays mirror the first page for older readers.
        /// </summary>
        [TestMethod]
        public void MultiPageRoundTripsPerSurface()
        {
            TmForgeJsonFormat format = new TmForgeJsonFormat();

            ThreatModel model;
            using (MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(MultiPageJson)))
            {
                model = format.Read(input);
            }

            Assert.AreEqual(2, model.DrawingSurfaceList.Count);
            Assert.AreEqual("Context", model.DrawingSurfaceList[0].Header);
            Assert.AreEqual("Payments", model.DrawingSurfaceList[1].Header);
            Assert.AreEqual(2, model.DrawingSurfaceList[0].Borders.Count);
            Assert.AreEqual(1, model.DrawingSurfaceList[0].Lines.Count);
            Assert.AreEqual(2, model.DrawingSurfaceList[1].Borders.Count);
            Assert.AreEqual(1, model.DrawingSurfaceList[1].Lines.Count);

            string json;
            using (MemoryStream output = new MemoryStream())
            {
                format.Write(model, output);
                json = Encoding.UTF8.GetString(output.ToArray());
            }

            using (JsonDocument parsed = JsonDocument.Parse(json))
            {
                JsonElement root = parsed.RootElement;
                JsonElement diagrams = root.GetProperty("diagrams");
                Assert.AreEqual(2, diagrams.GetArrayLength());
                Assert.AreEqual("Context", diagrams[0].GetProperty("name").GetString());
                Assert.AreEqual("Payments", diagrams[1].GetProperty("name").GetString());
                Assert.AreEqual(2, diagrams[0].GetProperty("elements").GetArrayLength());
                Assert.AreEqual(1, diagrams[0].GetProperty("flows").GetArrayLength());
                Assert.AreEqual(2, diagrams[1].GetProperty("elements").GetArrayLength());
                Assert.AreEqual(1, diagrams[1].GetProperty("flows").GetArrayLength());

                // Top-level arrays mirror the first page for single-page readers.
                Assert.AreEqual(2, root.GetProperty("elements").GetArrayLength());
                Assert.AreEqual(1, root.GetProperty("flows").GetArrayLength());
            }

            StringAssert.Contains(json, "Gateway");
            StringAssert.Contains(json, "Payment Svc");
            StringAssert.Contains(json, "Ledger");
        }

        /// <summary>
        /// A single-page model writes no <c>diagrams</c> array, keeping the wire shape backward
        /// compatible with existing single-page readers and files.
        /// </summary>
        [TestMethod]
        public void SinglePageOmitsDiagramsArray()
        {
            TmForgeJsonFormat format = new TmForgeJsonFormat();

            ThreatModel model;
            using (MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(SampleJson)))
            {
                model = format.Read(input);
            }

            string json;
            using (MemoryStream output = new MemoryStream())
            {
                format.Write(model, output);
                json = Encoding.UTF8.GetString(output.ToArray());
            }

            using (JsonDocument parsed = JsonDocument.Parse(json))
            {
                Assert.IsFalse(parsed.RootElement.TryGetProperty("diagrams", out _));
            }
        }

        /// <summary>
        /// The per-model analysis selection round-trips through the analysis-aware Write overload
        /// and <see cref="TmForgeJsonFormat.TryReadAnalysis"/>.
        /// </summary>
        [TestMethod]
        public void AnalysisRoundTrips()
        {
            ThreatModel model = new ThreatModel();
            TmForgeJsonAnalysis analysis = new TmForgeJsonAnalysis
            {
                DisabledPacks = new[] { "stride-completeness" },
                DisabledRuleIds = new[] { "TM1002" },
            };

            byte[] bytes;
            using (MemoryStream output = new MemoryStream())
            {
                new TmForgeJsonFormat().Write(model, output, analysis);
                bytes = output.ToArray();
            }

            using (MemoryStream input = new MemoryStream(bytes))
            {
                bool hasSelection = TmForgeJsonFormat.TryReadAnalysis(
                    input,
                    out IReadOnlyList<string> packs,
                    out IReadOnlyList<string> ruleIds);

                Assert.IsTrue(hasSelection);
                Assert.AreEqual(1, packs.Count);
                Assert.AreEqual("stride-completeness", packs[0]);
                Assert.AreEqual(1, ruleIds.Count);
                Assert.AreEqual("TM1002", ruleIds[0]);
            }
        }

        /// <summary>
        /// A document written without an analysis selection reports none on read.
        /// </summary>
        [TestMethod]
        public void AnalysisAbsentWhenNotWritten()
        {
            ThreatModel model = new ThreatModel();

            byte[] bytes;
            using (MemoryStream output = new MemoryStream())
            {
                new TmForgeJsonFormat().Write(model, output);
                bytes = output.ToArray();
            }

            using (MemoryStream input = new MemoryStream(bytes))
            {
                bool hasSelection = TmForgeJsonFormat.TryReadAnalysis(
                    input,
                    out IReadOnlyList<string> packs,
                    out IReadOnlyList<string> ruleIds);

                Assert.IsFalse(hasSelection);
                Assert.AreEqual(0, packs.Count);
                Assert.AreEqual(0, ruleIds.Count);
            }
        }
    }
}
