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
        /// The per-model validation selection round-trips through the validation-aware Write overload
        /// and <see cref="TmForgeJsonFormat.TryReadValidation"/>.
        /// </summary>
        [TestMethod]
        public void ValidationRoundTrips()
        {
            ThreatModel model = new ThreatModel();
            TmForgeJsonValidation validation = new TmForgeJsonValidation
            {
                DisabledPacks = new[] { "stride-completeness" },
                DisabledRuleIds = new[] { "TM1002" },
            };

            byte[] bytes;
            using (MemoryStream output = new MemoryStream())
            {
                new TmForgeJsonFormat().Write(model, output, validation);
                bytes = output.ToArray();
            }

            using (MemoryStream input = new MemoryStream(bytes))
            {
                bool hasSelection = TmForgeJsonFormat.TryReadValidation(
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
        /// A document written without a validation selection reports none on read.
        /// </summary>
        [TestMethod]
        public void ValidationAbsentWhenNotWritten()
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
                bool hasSelection = TmForgeJsonFormat.TryReadValidation(
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
