namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests that <c>analyze --reportFolder</c> writes the versioned analysis document.
    /// </summary>
    /// <remarks>
    /// The CLI is the only producer that sees suppressions, so it is the only one that can report the
    /// <c>suppressed</c> disposition — and the one place the document and the SARIF from the same run
    /// can be checked against each other.
    /// </remarks>
    [TestClass]
    public class AnalyzeAnalysisDocumentTest
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

        /// <summary>Gets or sets the working directory for one test.</summary>
        private string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>Creates the working directory.</summary>
        [TestInitialize]
        public void Initialize()
        {
            this.WorkingDirectory = Path.Join(Path.GetTempPath(), "tmforge-evidence-" + Guid.NewGuid().ToString("N"));
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

        /// <summary>The report folder gains a versioned document with a disposition on every finding.</summary>
        [TestMethod]
        public void ReportFolderContainsTheAnalysisDocument()
        {
            JsonElement document = this.Analyze(out _);

            Assert.AreEqual("tmforge-analysis", document.GetProperty("schema").GetString());
            Assert.AreEqual(1, document.GetProperty("version").GetInt32());

            string modelFingerprint = document.GetProperty("model").GetProperty("fingerprint").GetString() ?? string.Empty;
            string analyzerFingerprint = document.GetProperty("analyzer").GetProperty("fingerprint").GetString() ?? string.Empty;
            Assert.IsTrue(modelFingerprint.StartsWith("sha256:", StringComparison.Ordinal));
            Assert.IsTrue(analyzerFingerprint.StartsWith("sha256:", StringComparison.Ordinal));

            JsonElement findings = document.GetProperty("findings");
            Assert.IsTrue(findings.GetArrayLength() > 0);
            foreach (JsonElement finding in findings.EnumerateArray())
            {
                Assert.IsFalse(string.IsNullOrEmpty(finding.GetProperty("disposition").GetString()));
                Assert.IsFalse(string.IsNullOrEmpty(finding.GetProperty("id").GetString()));
            }
        }

        /// <summary>
        /// The document and the SARIF describe the same run, so a finding has one identity across both
        /// artifacts rather than two that a consumer would have to reconcile.
        /// </summary>
        [TestMethod]
        public void DocumentIdsMatchTheSarifFingerprints()
        {
            JsonElement document = this.Analyze(out string reportFolder);

            HashSet<string> documentIds = document.GetProperty("findings").EnumerateArray()
                .Select(finding => finding.GetProperty("id").GetString() ?? string.Empty)
                .ToHashSet(StringComparer.Ordinal);

            using JsonDocument sarif = JsonDocument.Parse(
                File.ReadAllText(Path.Join(reportFolder, "model.sarif")));
            HashSet<string> sarifIds = sarif.RootElement.GetProperty("runs")[0].GetProperty("results").EnumerateArray()
                .Select(result => result.GetProperty("partialFingerprints")
                    .GetProperty("tmforgeFindingId/v1").GetString() ?? string.Empty)
                .ToHashSet(StringComparer.Ordinal);

            CollectionAssert.AreEquivalent(documentIds.ToArray(), sarifIds.ToArray());
        }

        /// <summary>Two runs over the same model produce byte-identical documents.</summary>
        [TestMethod]
        public void DocumentIsByteStableAcrossRuns()
        {
            this.Analyze(out string first);
            this.Analyze(out string second, folderName: "reports2");

            Assert.AreEqual(
                File.ReadAllText(Path.Join(first, "model.analysis.json")),
                File.ReadAllText(Path.Join(second, "model.analysis.json")));
        }

        /// <summary>
        /// A suppressed finding is recorded rather than dropped: it keeps its identity so a consumer
        /// can see it is still there and deliberately silenced, and it loses its threat link because a
        /// suppression says this one does not count.
        /// </summary>
        [TestMethod]
        public void SuppressedFindingIsRecordedWithoutAThreatLink()
        {
            string input = this.WriteInput();
            string unsuppressed = Path.Join(this.WorkingDirectory, "before");
            Run(new[] { input, "--reportFolder", unsuppressed });

            JsonElement target = ReadDocument(unsuppressed).GetProperty("findings").EnumerateArray()
                .First(finding => finding.GetProperty("disposition").GetString() != "hygiene");
            string ruleId = target.GetProperty("ruleId").GetString() ?? string.Empty;
            string findingId = target.GetProperty("id").GetString() ?? string.Empty;

            string suppressionPath = Path.Join(this.WorkingDirectory, "suppress.json");
            File.WriteAllText(suppressionPath, this.BuildSuppression(ruleId, input));

            string suppressed = Path.Join(this.WorkingDirectory, "after");
            Run(new[] { input, "--suppressionFile", suppressionPath, "--reportFolder", suppressed });

            JsonElement finding = ReadDocument(suppressed).GetProperty("findings").EnumerateArray()
                .Single(entry => entry.GetProperty("id").GetString() == findingId);

            Assert.AreEqual("suppressed", finding.GetProperty("disposition").GetString());
            Assert.IsFalse(
                finding.TryGetProperty("threatId", out JsonElement threat) &&
                threat.ValueKind != JsonValueKind.Null,
                "A suppressed finding must not also claim a place in the threat register.");
        }

        private static void Run(string[] args)
        {
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;
            using StringWriter outWriter = new StringWriter();
            using StringWriter errorWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);
            try
            {
                AnalyzeCommand.Run(args);
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }
        }

        private static JsonElement ReadDocument(string reportFolder)
        {
            string path = Path.Join(reportFolder, "model.analysis.json");
            Assert.IsTrue(File.Exists(path), $"Expected an analysis document at {path}.");
            return JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();
        }

        private JsonElement Analyze(out string reportFolder, string folderName = "reports")
        {
            string input = this.WriteInput();
            reportFolder = Path.Join(this.WorkingDirectory, folderName);
            Run(new[] { input, "--reportFolder", reportFolder });
            return ReadDocument(reportFolder);
        }

        private string BuildSuppression(string ruleId, string modelPath)
        {
            // Suppressions are scoped to a rule on a specific element of a specific diagram, so the
            // entity is named by the display text the analyzer reports.
            string listing = Path.Join(this.WorkingDirectory, "listing");
            Run(new[] { modelPath, "--reportFolder", listing });

            using JsonDocument report = JsonDocument.Parse(
                File.ReadAllText(Path.Join(listing, "model.json")));
            JsonElement message = report.RootElement.GetProperty("ruleReports").EnumerateArray()
                .First(rule => rule.GetProperty("id").GetString() == ruleId)
                .GetProperty("messages")[0];

            return JsonSerializer.Serialize(new
            {
                files = new[]
                {
                    new
                    {
                        file = Path.GetFileName(modelPath),
                        suppressions = new[]
                        {
                            new
                            {
                                rule = ruleId,
                                model = "Diagram 1",
                                target = message.GetProperty("entity").GetString(),
                                justification = "accepted for this test",
                            },
                        },
                    },
                },
            });
        }

        private string WriteInput()
        {
            string input = Path.Join(this.WorkingDirectory, "model.json");
            File.WriteAllText(input, SampleJson);
            return input;
        }
    }
}
