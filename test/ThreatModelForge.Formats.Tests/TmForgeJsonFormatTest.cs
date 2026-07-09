namespace ThreatModelForge.Formats.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
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
        /// Verifies that the authored width and height of a component (not only a trust boundary) are
        /// applied on read, so an element keeps the size the canvas gave it instead of shrinking to the
        /// stencil's default size on the round trip.
        /// </summary>
        [TestMethod]
        public void ReadAppliesAuthoredComponentSize()
        {
            string json = "{\"schema\":\"tmforge-json\",\"version\":\"0.1\",\"elements\":[" +
                "{\"id\":\"p1\",\"kind\":\"process\",\"name\":\"Web App\",\"x\":100,\"y\":100,\"width\":140,\"height\":132}]}";

            ThreatModel model;
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                model = new TmForgeJsonFormat().Read(stream);
            }

            StencilEllipse element = (StencilEllipse)model.DrawingSurfaceList[0].Borders.Values.Single();
            Assert.AreEqual(140, element.Width);
            Assert.AreEqual(132, element.Height);
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

        /// <summary>
        /// A tmforge-json document carrying a risk-acceptance triage overlay seeds the model's threat
        /// register on read, so acceptance recorded in Studio survives an export to <c>.tm7</c> or a
        /// CLI round-trip (the register natively round-trips in <c>.tm7</c>). The seeded threat carries
        /// the accepted state, the justification, and the target/rule parsed from its register id.
        /// </summary>
        [TestMethod]
        public void AcceptedTriageSeedsRegisterOnRead()
        {
            System.Guid target = System.Guid.NewGuid();
            string threatId = target.ToString("N") + ":TM1013";
            string json = "{\"schema\":\"tmforge-json\",\"version\":\"0.1\",\"elements\":[]," +
                "\"flows\":[],\"threats\":[{\"id\":\"" + threatId +
                "\",\"state\":\"Accepted\",\"justification\":\"Compensating control in place.\"}]}";

            ThreatModel model;
            using (MemoryStream input = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                model = new TmForgeJsonFormat().Read(input);
            }

            Assert.IsTrue(model.AllThreatsDictionary.TryGetValue(threatId, out Threat? threat));
            Assert.AreEqual(ThreatState.NotApplicable, threat!.State);
            Assert.AreEqual("Compensating control in place.", threat.StateInformation);
            Assert.AreEqual("TM1013", threat.TypeId);
            Assert.AreEqual(target, threat.SourceGuid);
            Assert.AreEqual(threatId, threat.InteractionKey);
        }

        /// <summary>
        /// An accepted threat in the model's register writes back as an <c>Accepted</c> triage entry
        /// on the document, and reading that document restores the acceptance — the symmetric
        /// round-trip that carries Studio acceptance across the wire and the CLI.
        /// </summary>
        [TestMethod]
        public void AcceptedTriageRoundTrips()
        {
            System.Guid target = System.Guid.NewGuid();
            string threatId = target.ToString("N") + ":TM1023";
            ThreatModel source = new ThreatModel();
            source.AllThreatsDictionary[threatId] = new Threat
            {
                Id = 1,
                TypeId = "TM1023",
                State = ThreatState.NotApplicable,
                InteractionKey = threatId,
                StateInformation = "Accepted by security review.",
            };

            byte[] bytes;
            using (MemoryStream output = new MemoryStream())
            {
                new TmForgeJsonFormat().Write(source, output);
                bytes = output.ToArray();
            }

            using (JsonDocument parsed = JsonDocument.Parse(bytes))
            {
                JsonElement threats = parsed.RootElement.GetProperty("threats");
                Assert.AreEqual(1, threats.GetArrayLength());
                Assert.AreEqual(threatId, threats[0].GetProperty("id").GetString());
                Assert.AreEqual("Accepted", threats[0].GetProperty("state").GetString());
                Assert.AreEqual("Accepted by security review.", threats[0].GetProperty("justification").GetString());
            }

            ThreatModel reread;
            using (MemoryStream input = new MemoryStream(bytes))
            {
                reread = new TmForgeJsonFormat().Read(input);
            }

            Assert.IsTrue(reread.AllThreatsDictionary.TryGetValue(threatId, out Threat? threat));
            Assert.AreEqual(ThreatState.NotApplicable, threat!.State);
            Assert.AreEqual("Accepted by security review.", threat.StateInformation);
        }

        /// <summary>
        /// A model with no accepted threats writes no <c>threats</c> overlay, keeping the wire shape
        /// backward compatible for the common (untriaged) case.
        /// </summary>
        [TestMethod]
        public void TriageAbsentWhenNothingAccepted()
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
                Assert.IsFalse(parsed.RootElement.TryGetProperty("threats", out _));
            }
        }

        /// <summary>
        /// A manually-authored threat (keyed <c>manual:{guid}</c>) round-trips through the overlay in
        /// full: its category, title, description, mitigation, priority, scope, and state survive a
        /// write and re-read, so a threat the author created by hand is not lost on save.
        /// </summary>
        [TestMethod]
        public void ManualThreatRoundTrips()
        {
            System.Guid node = System.Guid.NewGuid();
            string id = "manual:" + System.Guid.NewGuid().ToString("N");
            ThreatModel source = new ThreatModel();
            source.AllThreatsDictionary[id] = new Threat
            {
                Id = 1,
                State = ThreatState.NeedsInvestigation,
                InteractionKey = id,
                SourceGuid = node,
                Title = "Stolen session token",
                UserThreatCategory = "Spoofing",
                UserThreatDescription = "An attacker replays a captured bearer token.",
                Priority = "High",
                StateInformation = "Under review.",
                Properties = new Dictionary<string, string> { ["Mitigation"] = "Bind tokens to the client." },
            };

            byte[] bytes;
            using (MemoryStream output = new MemoryStream())
            {
                new TmForgeJsonFormat().Write(source, output);
                bytes = output.ToArray();
            }

            using (JsonDocument parsed = JsonDocument.Parse(bytes))
            {
                JsonElement entry = parsed.RootElement.GetProperty("threats")[0];
                Assert.AreEqual(id, entry.GetProperty("id").GetString());
                Assert.IsTrue(entry.GetProperty("manual").GetBoolean());
                Assert.AreEqual("NeedsInvestigation", entry.GetProperty("state").GetString());
                Assert.AreEqual("Spoofing", entry.GetProperty("category").GetString());
                Assert.AreEqual("Stolen session token", entry.GetProperty("title").GetString());
                Assert.AreEqual("Bind tokens to the client.", entry.GetProperty("mitigation").GetString());
                Assert.AreEqual(node.ToString(), entry.GetProperty("elementIds")[0].GetString());
            }

            ThreatModel reread;
            using (MemoryStream input = new MemoryStream(bytes))
            {
                reread = new TmForgeJsonFormat().Read(input);
            }

            Assert.IsTrue(reread.AllThreatsDictionary.TryGetValue(id, out Threat? threat));
            Assert.AreEqual(ThreatState.NeedsInvestigation, threat!.State);
            Assert.AreEqual("Stolen session token", threat.Title);
            Assert.AreEqual("Spoofing", threat.UserThreatCategory);
            Assert.AreEqual("An attacker replays a captured bearer token.", threat.UserThreatDescription);
            Assert.AreEqual("High", threat.Priority);
            Assert.AreEqual(node, threat.SourceGuid);
            Assert.IsNotNull(threat.Properties);
            Assert.AreEqual("Bind tokens to the client.", threat.Properties!["Mitigation"]);
        }

        /// <summary>
        /// An edited rule threat — one whose state moved to <c>Mitigated</c> with a description — round
        /// trips its author-owned fields on the overlay without storing the regenerable rule text, so
        /// the edit survives a save while the register stays sparse (no <c>manual</c> flag emitted).
        /// </summary>
        [TestMethod]
        public void EditedRuleThreatRoundTrips()
        {
            System.Guid target = System.Guid.NewGuid();
            string threatId = target.ToString("N") + ":TM1013";
            ThreatModel source = new ThreatModel();
            source.AllThreatsDictionary[threatId] = new Threat
            {
                Id = 1,
                TypeId = "TM1013",
                State = ThreatState.Mitigated,
                InteractionKey = threatId,
                SourceGuid = target,
                UserThreatDescription = "Handled by the WAF rule set.",
            };

            byte[] bytes;
            using (MemoryStream output = new MemoryStream())
            {
                new TmForgeJsonFormat().Write(source, output);
                bytes = output.ToArray();
            }

            using (JsonDocument parsed = JsonDocument.Parse(bytes))
            {
                JsonElement entry = parsed.RootElement.GetProperty("threats")[0];
                Assert.AreEqual("Mitigated", entry.GetProperty("state").GetString());
                Assert.AreEqual("Handled by the WAF rule set.", entry.GetProperty("description").GetString());
                Assert.IsFalse(entry.TryGetProperty("manual", out _));
            }

            ThreatModel reread;
            using (MemoryStream input = new MemoryStream(bytes))
            {
                reread = new TmForgeJsonFormat().Read(input);
            }

            Assert.IsTrue(reread.AllThreatsDictionary.TryGetValue(threatId, out Threat? threat));
            Assert.AreEqual(ThreatState.Mitigated, threat!.State);
            Assert.AreEqual("Handled by the WAF rule set.", threat.UserThreatDescription);
            Assert.AreEqual("TM1013", threat.TypeId);
        }
    }
}
