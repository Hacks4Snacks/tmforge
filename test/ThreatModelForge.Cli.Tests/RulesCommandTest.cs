namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Analysis;
    using ThreatModelForge.KnowledgeBase;

    /// <summary>Integration tests for <c>tmforge rules import</c>.</summary>
    [TestClass]
    public class RulesCommandTest
    {
        /// <summary>Gets or sets the isolated test directory.</summary>
        private string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>Creates an isolated test directory.</summary>
        [TestInitialize]
        public void Initialize()
        {
            this.WorkingDirectory = Path.Join(Path.GetTempPath(), "tmforge-rules-import-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.WorkingDirectory);
        }

        /// <summary>Removes the isolated test directory.</summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(this.WorkingDirectory))
            {
                Directory.Delete(this.WorkingDirectory, recursive: true);
            }
        }

        /// <summary>A complete template writes a strict-loadable pack and structured summary.</summary>
        [TestMethod]
        public void ImportWritesLoadablePackAndJsonSummary()
        {
            string input = this.WriteTemplate(includeInvalidThreat: false);
            string output = Path.Join(this.WorkingDirectory, "output.tmrules.json");

            (int exit, string stdout, _) = Capture(() => RulesCommand.Run(new[]
            {
                "import", "--from", input, "--out", output, "--pack-id", "medical-pack", "--json",
            }));

            Assert.AreEqual(0, exit);
            Assert.IsTrue(File.Exists(output));
            using (JsonDocument summary = JsonDocument.Parse(stdout))
            {
                JsonElement data = summary.RootElement.GetProperty("data");
                Assert.AreEqual("success", data.GetProperty("status").GetString());
                Assert.AreEqual("medical-pack", data.GetProperty("packId").GetString());
                Assert.AreEqual(1, data.GetProperty("sourceCount").GetInt32());
                Assert.AreEqual(1, data.GetProperty("emittedCount").GetInt32());
                Assert.AreEqual(0, data.GetProperty("skippedCount").GetInt32());
                Assert.AreEqual(1, data.GetProperty("categoryDistribution").GetProperty("privacy").GetInt32());
            }

            List<string> diagnostics = new List<string>();
            RuleBundle bundle = DeclarativeRuleProvider.LoadBundle(new[] { output }, diagnostics.Add);
            Assert.AreEqual(0, diagnostics.Count, string.Join(Environment.NewLine, diagnostics));
            Assert.AreEqual(1, bundle.Rules.Count);
        }

        /// <summary>Non-strict import writes all representable threats and reports skipped source material.</summary>
        [TestMethod]
        public void PartialImportWritesRepresentableRules()
        {
            string input = this.WriteTemplate(includeInvalidThreat: true);
            string output = Path.Join(this.WorkingDirectory, "partial.tmrules.json");

            (int exit, string stdout, _) = Capture(() => RulesCommand.Run(new[]
            {
                "import", "--from", input, "--out", output, "--json",
            }));

            Assert.AreEqual(0, exit);
            Assert.IsTrue(File.Exists(output));
            using JsonDocument summary = JsonDocument.Parse(stdout);
            JsonElement data = summary.RootElement.GetProperty("data");
            Assert.AreEqual("partial", data.GetProperty("status").GetString());
            Assert.AreEqual(2, data.GetProperty("sourceCount").GetInt32());
            Assert.AreEqual(1, data.GetProperty("emittedCount").GetInt32());
            Assert.AreEqual(1, data.GetProperty("skippedCount").GetInt32());
            JsonElement diagnostic = data.GetProperty("diagnostics")[0];
            Assert.AreEqual("BAD-1", diagnostic.GetProperty("sourceThreatId").GetString());
            StringAssert.Contains(diagnostic.GetProperty("sourceExpression").GetString() ?? string.Empty, "not valid syntax");
        }

        /// <summary>Strict import refuses to write when any threat cannot be represented exactly.</summary>
        [TestMethod]
        public void StrictImportFailsWithoutWritingPartialPack()
        {
            string input = this.WriteTemplate(includeInvalidThreat: true);
            string output = Path.Join(this.WorkingDirectory, "strict.tmrules.json");

            (int exit, string stdout, _) = Capture(() => RulesCommand.Run(new[]
            {
                "import", "--from", input, "--out", output, "--strict", "--json",
            }));

            Assert.AreEqual(2, exit);
            Assert.IsFalse(File.Exists(output));
            using JsonDocument summary = JsonDocument.Parse(stdout);
            JsonElement data = summary.RootElement.GetProperty("data");
            Assert.AreEqual("failed", data.GetProperty("status").GetString());
            Assert.AreEqual(JsonValueKind.Null, data.GetProperty("output").ValueKind);
            Assert.AreEqual(1, data.GetProperty("skippedCount").GetInt32());
        }

        /// <summary>Import refuses to replace the source template with the generated pack.</summary>
        [TestMethod]
        public void ImportRejectsSameInputAndOutputPath()
        {
            string input = this.WriteTemplate(includeInvalidThreat: false);
            byte[] before = File.ReadAllBytes(input);

            (int exit, _, string stderr) = Capture(() => RulesCommand.Run(new[]
            {
                "import", "--from", input, "--out", input,
            }));

            Assert.AreEqual(1, exit);
            StringAssert.Contains(stderr, "must be different");
            CollectionAssert.AreEqual(before, File.ReadAllBytes(input));
        }

        /// <summary>Symlink aliases cannot bypass source-file replacement protection.</summary>
        [TestMethod]
        public void ImportRejectsSymlinkAliasesToInput()
        {
            string input = this.WriteTemplate(includeInvalidThreat: false);
            byte[] before = File.ReadAllBytes(input);
            string link = Path.Join(this.WorkingDirectory, "template-link.tb7");
            File.CreateSymbolicLink(link, input);

            (int fileExit, _, _) = Capture(() => RulesCommand.Run(new[]
            {
                "import", "--from", link, "--out", input,
            }));

            string realDirectory = Path.Join(this.WorkingDirectory, "real");
            Directory.CreateDirectory(realDirectory);
            string realInput = Path.Join(realDirectory, "source.tb7");
            File.Copy(input, realInput);
            string directoryLink = Path.Join(this.WorkingDirectory, "linked-directory");
            Directory.CreateSymbolicLink(directoryLink, realDirectory);
            (int parentExit, _, _) = Capture(() => RulesCommand.Run(new[]
            {
                "import", "--from", realInput, "--out", Path.Join(directoryLink, "source.tb7"),
            }));

            Assert.AreEqual(1, fileExit);
            Assert.AreEqual(1, parentExit);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(input));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(realInput));
        }

        /// <summary>Path-resolution failures stay inside the command and preserve the JSON contract.</summary>
        [TestMethod]
        public void PathFailureReturnsJsonErrorEnvelope()
        {
            string input = this.WriteTemplate(includeInvalidThreat: false);
            string cycleA = Path.Join(this.WorkingDirectory, "cycle-a");
            string cycleB = Path.Join(this.WorkingDirectory, "cycle-b");
            Directory.CreateSymbolicLink(cycleA, cycleB);
            Directory.CreateSymbolicLink(cycleB, cycleA);

            (int exit, string stdout, _) = Capture(() => RulesCommand.Run(new[]
            {
                "import", "--from", input, "--out", Path.Join(cycleA, "pack.tmrules.json"), "--json",
            }));

            Assert.AreEqual(1, exit);
            using JsonDocument result = JsonDocument.Parse(stdout);
            JsonElement data = result.RootElement.GetProperty("data");
            Assert.AreEqual("failed", data.GetProperty("status").GetString());
            Assert.IsFalse(string.IsNullOrWhiteSpace(data.GetProperty("error").GetString()));
        }

        /// <summary>A missing value cannot consume a following flag and silently disable strict mode.</summary>
        [TestMethod]
        public void MissingOptionValueIsRejected()
        {
            string input = this.WriteTemplate(includeInvalidThreat: true);

            (int exit, string stdout, _) = Capture(() => RulesCommand.Run(new[]
            {
                "import", "--from", input, "--out", "--strict", "--json",
            }));

            Assert.AreEqual(1, exit);
            using JsonDocument result = JsonDocument.Parse(stdout);
            JsonElement data = result.RootElement.GetProperty("data");
            Assert.AreEqual("failed", data.GetProperty("status").GetString());
            StringAssert.Contains(data.GetProperty("error").GetString() ?? string.Empty, "Missing value");
            Assert.IsFalse(File.Exists(Path.Join(this.WorkingDirectory, "--strict")));
        }

        private static (int Exit, string Stdout, string Stderr) Capture(Func<int> run)
        {
            using StringWriter outWriter = new StringWriter();
            using StringWriter errorWriter = new StringWriter();
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);
            try
            {
                return (run(), outWriter.ToString(), errorWriter.ToString());
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }
        }

        private string WriteTemplate(bool includeInvalidThreat)
        {
            KnowledgeBaseData knowledgeBase = new KnowledgeBaseData
            {
                Manifest = new Manifest
                {
                    Id = Guid.NewGuid(),
                    Name = "Medical Template",
                    Version = "1.0",
                },
            };
            knowledgeBase.ThreatCategories.Add(new ThreatCategory { Id = "privacy", Name = "Privacy" });
            knowledgeBase.GenericElements.Add(new ElementType { Id = "GE.P", Name = "Process", ParentId = "ROOT" });
            knowledgeBase.ThreatTypes.Add(new ThreatType
            {
                Id = "GOOD-1",
                Category = "privacy",
                ShortTitle = "Privacy risk",
                GenerationFilters = new GenerationFilters { Include = "source is 'GE.P'" },
            });
            if (includeInvalidThreat)
            {
                knowledgeBase.ThreatTypes.Add(new ThreatType
                {
                    Id = "BAD-1",
                    Category = "privacy",
                    ShortTitle = "Invalid threat",
                    GenerationFilters = new GenerationFilters { Include = "not valid syntax" },
                });
            }

            string path = Path.Join(this.WorkingDirectory, "template.tb7");
            knowledgeBase.Save(path);
            return path;
        }
    }
}
