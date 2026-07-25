namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <c>tmforge analysis validate</c> and for the optional taxonomy mapping on
    /// <c>tmforge analyze</c>.
    /// </summary>
    [TestClass]
    public class AnalysisCommandTest
    {
        private const string SampleJson =
            "{\"schema\":\"tmforge-json\",\"version\":\"0.1\"," +
            "\"elements\":[" +
            "{\"id\":\"s1\",\"kind\":\"datastore\",\"name\":\"Ledger\",\"x\":0,\"y\":0," +
            "\"properties\":{\"StoresLogData\":\"Yes\"}}," +
            "{\"id\":\"p1\",\"kind\":\"process\",\"name\":\"Checkout\",\"x\":200,\"y\":0}," +
            "{\"id\":\"e1\",\"kind\":\"external\",\"name\":\"Customer\",\"x\":400,\"y\":0}]," +
            "\"flows\":[" +
            "{\"id\":\"f1\",\"source\":\"e1\",\"target\":\"p1\",\"name\":\"order\"}," +
            "{\"id\":\"f2\",\"source\":\"p1\",\"target\":\"s1\",\"name\":\"write\"}]}";

        private const string EditedJson =
            "{\"schema\":\"tmforge-json\",\"version\":\"0.1\"," +
            "\"elements\":[" +
            "{\"id\":\"s1\",\"kind\":\"datastore\",\"name\":\"Ledger\",\"x\":0,\"y\":0}," +
            "{\"id\":\"p1\",\"kind\":\"process\",\"name\":\"Checkout\",\"x\":200,\"y\":0}]," +
            "\"flows\":[{\"id\":\"f2\",\"source\":\"p1\",\"target\":\"s1\",\"name\":\"write\"}]}";

        /// <summary>Gets or sets the working directory for one test.</summary>
        private string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>Creates the working directory.</summary>
        [TestInitialize]
        public void Initialize()
        {
            this.WorkingDirectory = Path.Join(Path.GetTempPath(), "tmforge-analysis-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.WorkingDirectory);
        }

        /// <summary>Removes the working directory.</summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(this.WorkingDirectory))
            {
                Directory.Delete(this.WorkingDirectory, recursive: true);
            }
        }

        /// <summary>A document the CLI just wrote validates.</summary>
        [TestMethod]
        public void FreshlyWrittenDocumentValidates()
        {
            string document = this.WriteAnalysis(SampleJson);

            (int exit, string stdout) = Run(new[] { "validate", document });

            Assert.AreEqual(0, exit);
            StringAssert.Contains(stdout, "valid tmforge-analysis v1");
        }

        /// <summary>
        /// Checked against the model it describes, a current document is sound and the exit code says
        /// so, which is what lets a gate depend on it.
        /// </summary>
        [TestMethod]
        public void DocumentValidatesAgainstItsOwnModel()
        {
            string model = this.WriteModel("model.json", SampleJson);
            string document = this.WriteAnalysis(SampleJson);

            (int exit, _) = Run(new[] { "validate", document, "--model", model });

            Assert.AreEqual(0, exit);
        }

        /// <summary>
        /// A coherent document that describes a model which has since changed is reported as stale.
        /// This is the failure the fingerprints exist for: a clean report about last month's
        /// architecture is more dangerous than an obviously broken one.
        /// </summary>
        [TestMethod]
        public void StaleDocumentIsReported()
        {
            string document = this.WriteAnalysis(SampleJson);
            string edited = this.WriteModel("edited.json", EditedJson);

            (int exit, string stdout) = Run(new[] { "validate", document, "--model", edited, "--json" });

            Assert.AreEqual(2, exit);
            using JsonDocument envelope = JsonDocument.Parse(stdout);
            JsonElement data = envelope.RootElement.GetProperty("data");
            Assert.AreEqual("invalid", data.GetProperty("status").GetString());
            Assert.IsTrue(data.GetProperty("stale").GetBoolean());
        }

        /// <summary>A caller pinning a version it was not written against is refused.</summary>
        [TestMethod]
        public void PinnedVersionMismatchIsRefused()
        {
            string document = this.WriteAnalysis(SampleJson);

            (int exit, _) = Run(new[] { "validate", document, "--expect-version", "2" });

            Assert.AreEqual(2, exit);
        }

        /// <summary>Pointing the command at a different artifact is refused rather than misread.</summary>
        [TestMethod]
        public void FindingsReportIsNotAnAnalysisDocument()
        {
            this.WriteAnalysis(SampleJson);
            string findings = Path.Join(this.WorkingDirectory, "reports", "model.json");

            (int exit, _) = Run(new[] { "validate", findings });

            Assert.AreEqual(2, exit);
        }

        /// <summary>
        /// A taxonomy mapping annotates the evidence with the engagement's ids, and leaves every rule it
        /// does not name with nothing. Detection is untouched: the finding set is identical either way.
        /// </summary>
        [TestMethod]
        public void TaxonomyMapsOnlyTheRulesItNames()
        {
            string plain = this.WriteAnalysis(SampleJson);
            JsonElement withoutMap = ReadDocument(plain);
            string mappedRule = withoutMap.GetProperty("findings")[0].GetProperty("ruleId").GetString() ?? string.Empty;

            string taxonomy = Path.Join(this.WorkingDirectory, "taxonomy.json");
            File.WriteAllText(
                taxonomy,
                "{\"schema\":\"tmforge-taxonomy\",\"version\":1,\"rules\":{\"" + mappedRule + "\":[\"ACME-T-017\"]}}");

            string model = this.WriteModel("model.json", SampleJson);
            string reports = Path.Join(this.WorkingDirectory, "mapped");
            RunAnalyze(new[] { model, "--taxonomy", taxonomy, "--reportFolder", reports });
            JsonElement withMap = ReadDocument(Path.Join(reports, "model.analysis.json"));

            // The same findings, in the same order, with the same identities.
            CollectionAssert.AreEqual(
                withoutMap.GetProperty("findings").EnumerateArray().Select(Identity).ToArray(),
                withMap.GetProperty("findings").EnumerateArray().Select(Identity).ToArray());

            foreach (JsonElement finding in withMap.GetProperty("findings").EnumerateArray())
            {
                string[] canonical = finding.GetProperty("canonicalIds").EnumerateArray()
                    .Select(id => id.GetString() ?? string.Empty)
                    .ToArray();

                if (finding.GetProperty("ruleId").GetString() == mappedRule)
                {
                    CollectionAssert.AreEqual(new[] { "ACME-T-017" }, canonical);
                }
                else
                {
                    Assert.AreEqual(0, canonical.Length, "An unmapped rule must not be given a guessed id.");
                }
            }
        }

        /// <summary>An unreadable mapping is an error, not a silent omission of the ids asked for.</summary>
        [TestMethod]
        public void UnreadableTaxonomyIsAnError()
        {
            string taxonomy = Path.Join(this.WorkingDirectory, "taxonomy.json");
            File.WriteAllText(taxonomy, "{\"schema\":\"tmforge-taxonomy\",\"version\":42}");
            string model = this.WriteModel("model.json", SampleJson);

            int exit = RunAnalyze(new[]
            {
                model, "--taxonomy", taxonomy, "--reportFolder", Path.Join(this.WorkingDirectory, "out"),
            });

            Assert.AreEqual(1, exit);
        }

        private static string Identity(JsonElement finding) => finding.GetProperty("id").GetString() ?? string.Empty;

        private static JsonElement ReadDocument(string path)
        {
            Assert.IsTrue(File.Exists(path), $"Expected an analysis document at {path}.");
            return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
        }

        private static (int Exit, string Stdout) Run(string[] args) => Capture(() => AnalysisCommand.Run(args));

        private static int RunAnalyze(string[] args) => Capture(() => AnalyzeCommand.Run(args)).Exit;

        private static (int Exit, string Stdout) Capture(Func<int> action)
        {
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;
            using StringWriter outWriter = new StringWriter();
            using StringWriter errorWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);
            try
            {
                return (action(), outWriter.ToString());
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }
        }

        private string WriteModel(string name, string json)
        {
            string path = Path.Join(this.WorkingDirectory, name);
            File.WriteAllText(path, json);
            return path;
        }

        private string WriteAnalysis(string json)
        {
            string model = this.WriteModel("model.json", json);
            string reports = Path.Join(this.WorkingDirectory, "reports");
            RunAnalyze(new[] { model, "--reportFolder", reports });
            return Path.Join(reports, "model.analysis.json");
        }
    }
}
