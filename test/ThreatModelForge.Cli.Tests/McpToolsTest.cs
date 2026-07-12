namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Engine;

    /// <summary>
    /// Unit tests for the <c>tmforge mcp</c> tool layer: the mapping from agent-friendly parameters
    /// (kind nouns, property maps, file paths) onto the engine and authoring facades. The MCP protocol
    /// wiring itself is exercised by a stdio smoke test; these verify the thin tool adapters.
    /// </summary>
    [TestClass]
    public class McpToolsTest
    {
        /// <summary>Gets or sets the working directory created for each test.</summary>
        private string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>Creates an isolated working directory for the test.</summary>
        [TestInitialize]
        public void Initialize()
        {
            this.WorkingDirectory = Path.Join(Path.GetTempPath(), "tmforge-mcp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.WorkingDirectory);
        }

        /// <summary>Removes the working directory after the test.</summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(this.WorkingDirectory))
            {
                Directory.Delete(this.WorkingDirectory, recursive: true);
            }
        }

        /// <summary>
        /// Verifies that <c>add</c> resolves the kind noun, marshals the property map, and returns the model.
        /// </summary>
        [TestMethod]
        public void Add_ResolvesKindNounAndStampsProperties()
        {
            AuthoringResultDto result = McpAuthoringTools.Add(
                model: null,
                kind: "process",
                name: "API",
                alias: "api",
                properties: new Dictionary<string, string> { ["AuthenticationScheme"] = "OAuth" });

            Assert.IsTrue(result.Success, result.Error);
            TmForgeElementDto element = result.Model!.Elements!.Single();
            Assert.AreEqual("OAuth", element.Properties["AuthenticationScheme"]);
        }

        /// <summary>
        /// Verifies that <c>add</c> with an unrecognized kind reports an error instead of throwing.
        /// </summary>
        [TestMethod]
        public void Add_UnknownKind_ReturnsError()
        {
            AuthoringResultDto result = McpAuthoringTools.Add(model: null, kind: "widget", name: "X");

            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.Error);
        }

        /// <summary>
        /// Verifies that a model built with <c>apply</c> threads into <c>analyze</c> and produces real findings.
        /// </summary>
        [TestMethod]
        public void Apply_ThenAnalyze_ThreadsModelThroughTools()
        {
            Manifest manifest = new Manifest
            {
                Elements = new List<ManifestElement>
                {
                    new ManifestElement { Alias = "p1", Kind = "process", Name = "Web" },
                    new ManifestElement { Alias = "e1", Kind = "external", Name = "User" },
                },
                Flows = new List<ManifestFlow> { new ManifestFlow { From = "e1", To = "p1", Name = "req" } },
            };

            ApplyResultDto applied = McpAuthoringTools.Apply(manifest, force: false);
            Assert.IsTrue(applied.Success, applied.Error);

            IReadOnlyList<FindingDto> findings = McpModelTools.Analyze(applied.Model!);
            Assert.IsFalse(findings.Any(finding => finding.Id == "engine-error"));
        }

        /// <summary>
        /// Verifies that <c>save</c> writes a model to disk and <c>read</c> loads it back.
        /// </summary>
        [TestMethod]
        public void Save_And_Read_RoundTripAModel()
        {
            AuthoringResultDto added = McpAuthoringTools.Add(model: null, kind: "process", name: "Web", alias: "web");
            ServiceProvider services = CreateServices(this.WorkingDirectory);
            string path = "model.tm7";

            McpSaveResult saved = McpModelTools.Save(added.Model!, path, services);
            Assert.IsTrue(File.Exists(Path.Join(this.WorkingDirectory, path)));
            Assert.AreEqual("tm7", saved.Format);
            Assert.IsTrue(saved.Bytes > 0);

            TmForgeModelDto reread = McpModelTools.Read(path, services);
            Assert.AreEqual(1, reread.Elements!.Count);
            Assert.AreEqual("Web", reread.Elements![0].Name);
        }

        /// <summary>Verifies that relative traversal cannot leave the configured MCP workspace root.</summary>
        [TestMethod]
        public void Read_RejectsPathTraversal()
        {
            ServiceProvider services = CreateServices(this.WorkingDirectory);

            _ = Assert.Throws<UnauthorizedAccessException>(() => McpModelTools.Read("../outside.tm7", services));
        }

        /// <summary>Verifies that MCP writes require a registered threat-model file extension.</summary>
        [TestMethod]
        public void Save_RejectsNonModelExtension()
        {
            AuthoringResultDto added = McpAuthoringTools.Add(model: null, kind: "process", name: "Web");
            ServiceProvider services = CreateServices(this.WorkingDirectory);

            _ = Assert.Throws<NotSupportedException>(() => McpModelTools.Save(added.Model!, "workflow.yml", services, "tm7"));
            _ = Assert.Throws<NotSupportedException>(() => McpModelTools.Save(added.Model!, "package.json", services, "tmforge-json"));
        }

        /// <summary>Verifies that MCP-specific options are removed before generic host parsing.</summary>
        [TestMethod]
        public void CommandOptions_ParseRootAndLimits()
        {
            bool parsed = McpCommand.TryParseOptions(
                new[]
                {
                    "--root=" + this.WorkingDirectory,
                    "--max-read-bytes",
                    "1234",
                    "--max-write-bytes=5678",
                    "--environment",
                    "Development",
                },
                out string root,
                out long maxReadBytes,
                out long maxWriteBytes,
                out string[] hostArgs,
                out string? error);

            Assert.IsTrue(parsed, error);
            Assert.AreEqual(this.WorkingDirectory, root);
            Assert.AreEqual(1234, maxReadBytes);
            Assert.AreEqual(5678, maxWriteBytes);
            CollectionAssert.AreEqual(new[] { "--environment", "Development" }, hostArgs);
        }

        /// <summary>Verifies that missing and invalid MCP option values fail before server startup.</summary>
        [TestMethod]
        public void CommandOptions_RejectInvalidValues()
        {
            bool missing = McpCommand.TryParseOptions(
                new[] { "--root" },
                out _,
                out _,
                out _,
                out _,
                out string? missingError);
            bool invalid = McpCommand.TryParseOptions(
                new[] { "--max-read-bytes", "0" },
                out _,
                out _,
                out _,
                out _,
                out string? invalidError);

            Assert.IsFalse(missing);
            StringAssert.Contains(missingError!, "Missing value");
            Assert.IsFalse(invalid);
            StringAssert.Contains(invalidError!, "positive integer");
        }

        /// <summary>Verifies that oversized direct MCP model text is rejected before engine work.</summary>
        [TestMethod]
        public void Analyze_RejectsOversizedModelString()
        {
            TmForgeModelDto model = new TmForgeModelDto
            {
                Elements = new[]
                {
                    new TmForgeElementDto { Id = "p1", Kind = "process", Name = new string('x', 65537) },
                },
            };

            _ = Assert.Throws<InvalidDataException>(() => McpModelTools.Analyze(model));
        }

        /// <summary>Verifies that physical top-level collections cannot hide behind diagrams.</summary>
        [TestMethod]
        public void Analyze_BudgetsTopLevelAndDiagramCollectionsTogether()
        {
            TmForgeElementDto[] elements = Enumerable.Range(0, 6000)
                .Select(index => new TmForgeElementDto { Id = "p" + index, Kind = "process" })
                .ToArray();
            TmForgeModelDto model = new TmForgeModelDto
            {
                Elements = elements,
                Diagrams = new[]
                {
                    new TmForgeDiagramDto { Id = "d1", Name = "Page 1", Elements = elements },
                },
            };

            _ = Assert.Throws<InvalidDataException>(() => McpModelTools.Analyze(model));
        }

        /// <summary>Verifies that merge operands share one pre-execution complexity budget.</summary>
        [TestMethod]
        public void Merge_RejectsCombinedOversizedInputs()
        {
            TmForgeModelDto ours = ModelWithElements(6000, "ours");
            TmForgeModelDto theirs = ModelWithElements(6000, "theirs");

            _ = Assert.Throws<InvalidDataException>(() => McpModelTools.Merge(ours, theirs));
        }

        /// <summary>Verifies that oversized direct authoring arguments are rejected before edits.</summary>
        [TestMethod]
        public void Add_RejectsOversizedDirectArgument()
        {
            _ = Assert.Throws<InvalidDataException>(() => McpAuthoringTools.Add(
                model: null,
                kind: "process",
                name: new string('x', 65537)));
        }

        /// <summary>Verifies that oversized manifests are rejected before materialization.</summary>
        [TestMethod]
        public void Apply_RejectsOversizedManifest()
        {
            Manifest manifest = new Manifest
            {
                Elements = Enumerable.Range(0, 10001)
                    .Select(index => new ManifestElement { Alias = "p" + index, Kind = "process" })
                    .ToList(),
            };

            _ = Assert.Throws<InvalidDataException>(() => McpAuthoringTools.Apply(manifest));
        }

        /// <summary>Verifies that a sibling whose name starts with the root name is still outside.</summary>
        [TestMethod]
        public void Read_RejectsSiblingPrefixPath()
        {
            string sibling = this.WorkingDirectory + "-other";
            Directory.CreateDirectory(sibling);
            try
            {
                string outside = Path.Join(sibling, "model.tm7");
                File.WriteAllBytes(outside, new byte[] { 1 });
                ServiceProvider services = CreateServices(this.WorkingDirectory);

                _ = Assert.Throws<UnauthorizedAccessException>(() => McpModelTools.Read(outside, services));
            }
            finally
            {
                Directory.Delete(sibling, recursive: true);
            }
        }

        /// <summary>Verifies that an in-root file symlink cannot redirect a read outside the root.</summary>
        [TestMethod]
        public void Read_RejectsFileSymlinkEscape()
        {
            string outside = Path.Join(Path.GetTempPath(), "tmforge-mcp-outside-" + Guid.NewGuid().ToString("N") + ".tm7");
            string link = Path.Join(this.WorkingDirectory, "linked.tm7");
            File.WriteAllBytes(outside, new byte[] { 1 });
            try
            {
                File.CreateSymbolicLink(link, outside);
                ServiceProvider services = CreateServices(this.WorkingDirectory);

                _ = Assert.Throws<UnauthorizedAccessException>(() => McpModelTools.Read("linked.tm7", services));
            }
            finally
            {
                File.Delete(link);
                File.Delete(outside);
            }
        }

        /// <summary>Verifies that a symlinked parent cannot redirect a new output outside the root.</summary>
        [TestMethod]
        public void Save_RejectsParentSymlinkEscape()
        {
            string outside = Path.Join(Path.GetTempPath(), "tmforge-mcp-outside-" + Guid.NewGuid().ToString("N"));
            string link = Path.Join(this.WorkingDirectory, "linked");
            Directory.CreateDirectory(outside);
            try
            {
                Directory.CreateSymbolicLink(link, outside);
                AuthoringResultDto added = McpAuthoringTools.Add(model: null, kind: "process", name: "Web");
                ServiceProvider services = CreateServices(this.WorkingDirectory);

                _ = Assert.Throws<UnauthorizedAccessException>(() => McpModelTools.Save(added.Model!, "linked/model.tm7", services));
                Assert.IsFalse(File.Exists(Path.Join(outside, "model.tm7")));
            }
            finally
            {
                Directory.Delete(link);
                Directory.Delete(outside, recursive: true);
            }
        }

        /// <summary>Verifies that the read limit is checked before an oversized file is buffered.</summary>
        [TestMethod]
        public void Read_RejectsOversizedFile()
        {
            File.WriteAllBytes(Path.Join(this.WorkingDirectory, "large.tm7"), new byte[17]);
            ServiceProvider services = CreateServices(this.WorkingDirectory, maxReadBytes: 16);

            _ = Assert.Throws<IOException>(() => McpModelTools.Read("large.tm7", services));
        }

        /// <summary>Verifies that a small compressed VSDX cannot expand beyond the read budget.</summary>
        [TestMethod]
        public void Read_RejectsVsdxExpansionBomb()
        {
            string path = Path.Join(this.WorkingDirectory, "large.vsdx");
            using (FileStream output = File.Create(path))
            using (ZipArchive archive = new ZipArchive(output, ZipArchiveMode.Create))
            {
                WriteZipEntry(archive, "visio/document.xml", "<VisioDocument/>");
                WriteZipEntry(archive, "visio/pages/page1.xml", new string('x', 4096));
            }

            Assert.IsTrue(new FileInfo(path).Length < 1024, "fixture must pass the compressed-byte limit");
            ServiceProvider services = CreateServices(this.WorkingDirectory, maxReadBytes: 1024);

            _ = Assert.Throws<InvalidDataException>(() => McpModelTools.Read("large.vsdx", services, "vsdx"));
        }

        /// <summary>Verifies that prefixed ZIP packages are rejected before package parsing.</summary>
        [TestMethod]
        public void Detect_RejectsPrefixedZipPackage()
        {
            AuthoringResultDto added = McpAuthoringTools.Add(model: null, kind: "process", name: "Web");
            ServiceProvider services = CreateServices(this.WorkingDirectory);
            _ = McpModelTools.Save(added.Model!, "model.vsdx", services);
            byte[] package = File.ReadAllBytes(Path.Join(this.WorkingDirectory, "model.vsdx"));
            File.WriteAllBytes(
                Path.Join(this.WorkingDirectory, "prefixed.vsdx"),
                new byte[] { 1, 2, 3, 4 }.Concat(package).ToArray());

            _ = Assert.Throws<InvalidDataException>(() => McpModelTools.Detect("prefixed.vsdx", services));
        }

        /// <summary>Verifies that the write limit is enforced before any output file is created.</summary>
        [TestMethod]
        public void Save_RejectsOversizedOutput()
        {
            AuthoringResultDto added = McpAuthoringTools.Add(model: null, kind: "process", name: "Web");
            ServiceProvider services = CreateServices(this.WorkingDirectory, maxWriteBytes: 16);

            _ = Assert.Throws<IOException>(() => McpModelTools.Save(added.Model!, "model.tm7", services));
            Assert.IsFalse(File.Exists(Path.Join(this.WorkingDirectory, "model.tm7")));
        }

        /// <summary>Verifies that VSDX save streams through the bounded output and reads back.</summary>
        [TestMethod]
        public void Save_And_Read_VsdxRoundTripsAModel()
        {
            AuthoringResultDto added = McpAuthoringTools.Add(
                model: null,
                kind: "process",
                name: "Web",
                alias: "web",
                properties: new Dictionary<string, string> { ["Owner's note"] = "Use 'strict' \"mode\" & verify" },
                force: true);
            ServiceProvider services = CreateServices(this.WorkingDirectory);

            McpSaveResult saved = McpModelTools.Save(added.Model!, "model.vsdx", services);
            TmForgeModelDto reread = McpModelTools.Read("model.vsdx", services, "vsdx");

            Assert.AreEqual("vsdx", saved.Format);
            Assert.IsTrue(saved.Bytes > 0);
            Assert.AreEqual("Web", reread.Elements!.Single().Name);
            Assert.AreEqual("Use 'strict' \"mode\" & verify", reread.Elements![0].Properties["Owner's note"]);
        }

        /// <summary>Verifies that save does not create caller-selected intermediate directories.</summary>
        [TestMethod]
        public void Save_RejectsMissingParentDirectory()
        {
            AuthoringResultDto added = McpAuthoringTools.Add(model: null, kind: "process", name: "Web");
            ServiceProvider services = CreateServices(this.WorkingDirectory);

            _ = Assert.Throws<FileNotFoundException>(() => McpModelTools.Save(added.Model!, "missing/model.tm7", services));
        }

        /// <summary>Verifies that an explicit format cannot disagree with the destination extension.</summary>
        [TestMethod]
        public void Save_RejectsFormatExtensionMismatch()
        {
            AuthoringResultDto added = McpAuthoringTools.Add(model: null, kind: "process", name: "Web");
            ServiceProvider services = CreateServices(this.WorkingDirectory);

            _ = Assert.Throws<NotSupportedException>(() => McpModelTools.Save(added.Model!, "model.tm7", services, "drawio"));
        }

        /// <summary>Verifies that save replaces a hard-link entry instead of truncating its outside inode.</summary>
        [TestMethod]
        public void Save_DoesNotWriteThroughHardLink()
        {
            string outside = Path.Join(Path.GetTempPath(), "tmforge-mcp-outside-" + Guid.NewGuid().ToString("N") + ".tm7");
            string destination = Path.Join(this.WorkingDirectory, "model.tm7");
            byte[] original = new byte[] { 1, 2, 3, 4 };
            File.WriteAllBytes(outside, original);
            try
            {
                CreateHardLink(destination, outside);
                AuthoringResultDto added = McpAuthoringTools.Add(model: null, kind: "process", name: "Web");
                ServiceProvider services = CreateServices(this.WorkingDirectory);

                McpSaveResult saved = McpModelTools.Save(added.Model!, "model.tm7", services);

                CollectionAssert.AreEqual(original, File.ReadAllBytes(outside));
                Assert.IsTrue(saved.Bytes > original.Length);
                TmForgeModelDto reread = McpModelTools.Read("model.tm7", services);
                Assert.AreEqual("Web", reread.Elements!.Single().Name);
            }
            finally
            {
                File.Delete(destination);
                File.Delete(outside);
            }
        }

        /// <summary>Verifies that replacing the configured root with a symlink is detected before I/O.</summary>
        [TestMethod]
        public void Read_RejectsRootReplacement()
        {
            ServiceProvider services = CreateServices(this.WorkingDirectory);
            string movedRoot = this.WorkingDirectory + "-moved";
            string outside = this.WorkingDirectory + "-outside";
            Directory.CreateDirectory(outside);
            File.WriteAllBytes(Path.Join(outside, "model.tm7"), new byte[] { 1 });
            Directory.Move(this.WorkingDirectory, movedRoot);
            Directory.CreateSymbolicLink(this.WorkingDirectory, outside);
            try
            {
                _ = Assert.Throws<UnauthorizedAccessException>(() => McpModelTools.Read("model.tm7", services));
            }
            finally
            {
                Directory.Delete(this.WorkingDirectory);
                Directory.Move(movedRoot, this.WorkingDirectory);
                Directory.Delete(outside, recursive: true);
            }
        }

        /// <summary>
        /// Verifies that the grounding tools return their catalogs (schema, rules, stencils, formats).
        /// </summary>
        [TestMethod]
        public void Grounding_Tools_ReturnCatalogs()
        {
            Assert.IsTrue(McpGroundingTools.ManifestSchema().Length > 0);
            Assert.IsTrue(McpGroundingTools.Rules().Count > 0);
            Assert.IsTrue(McpGroundingTools.PropertySchema().Count > 0);
            Assert.IsTrue(McpGroundingTools.Stencils().Count > 0);
            Assert.IsTrue(McpGroundingTools.Formats().Count > 0);
        }

        /// <summary>
        /// Verifies that a property map is marshaled into the <c>KEY=VALUE</c> assignment list.
        /// </summary>
        [TestMethod]
        public void ToAssignments_MarshalsPropertyMap()
        {
            List<string> assignments = McpToolSupport.ToAssignments(new Dictionary<string, string> { ["Protocol"] = "HTTPS", ["Port"] = "443" }).ToList();

            Assert.AreEqual(2, assignments.Count);
            CollectionAssert.Contains(assignments, "Protocol=HTTPS");
            CollectionAssert.Contains(assignments, "Port=443");
        }

        /// <summary>
        /// Verifies that <c>add_threat</c> records a manually-authored threat on the model's overlay.
        /// </summary>
        [TestMethod]
        public void AddThreat_CreatesManualOverlayEntry()
        {
            AuthoringResultDto result = McpAuthoringTools.AddThreat(
                model: null,
                title: "Config is world-writable",
                category: "Tampering",
                scope: "22222222-2222-4222-8222-222222222222",
                priority: "High");

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Id!.StartsWith("manual:", StringComparison.Ordinal));
            ThreatStateDto entry = result.Model!.Threats!.Single();
            Assert.IsTrue(entry.Manual == true);
            Assert.AreEqual("Tampering", entry.Category);
            Assert.AreEqual("Config is world-writable", entry.Title);
            Assert.AreEqual("22222222-2222-4222-8222-222222222222", entry.ElementIds!.Single());
        }

        /// <summary>
        /// Verifies that <c>edit_threat</c> records a rule-threat edit that the projection then applies.
        /// </summary>
        [TestMethod]
        public void EditThreat_RecordsEditAndProjectsIt()
        {
            TmForgeModelDto model = ModelWithSpoofingThreat();
            ThreatDto spoof = EngineService.GenerateThreats(model).First(threat => threat.RuleId == "TM1023");

            AuthoringResultDto result = McpAuthoringTools.EditThreat(model, spoof.Id, state: "Mitigated", priority: "Low");

            Assert.IsTrue(result.Success, result.Error);
            ThreatDto after = EngineService.GenerateThreats(result.Model!).First(threat => threat.Id == spoof.Id);
            Assert.AreEqual("Mitigated", after.State);
            Assert.AreEqual("Low", after.Priority);
        }

        /// <summary>
        /// Verifies that <c>remove_threat</c> deletes a manually-authored threat's overlay entry.
        /// </summary>
        [TestMethod]
        public void RemoveThreat_DeletesManualOverlayEntry()
        {
            AuthoringResultDto added = McpAuthoringTools.AddThreat(model: null, title: "X", category: "Repudiation");
            Assert.IsTrue(added.Success, added.Error);
            string id = added.Id!;

            AuthoringResultDto removed = McpAuthoringTools.RemoveThreat(added.Model!, id);

            Assert.IsTrue(removed.Success, removed.Error);
            Assert.IsNull(removed.Model!.Threats);
            Assert.AreEqual(id, removed.Removed!.Single());
        }

        private static TmForgeModelDto ModelWithSpoofingThreat()
        {
            const string externalId = "11111111-1111-4111-8111-111111111111";
            const string processId = "22222222-2222-4222-8222-222222222222";
            return new TmForgeModelDto
            {
                Schema = "tmforge-json",
                Version = "0.1",
                Elements = new[]
                {
                    new TmForgeElementDto { Id = externalId, Kind = "external", Name = "Client", X = 40, Y = 40 },
                    new TmForgeElementDto { Id = processId, Kind = "process", Name = "Gateway", X = 220, Y = 40 },
                },
                Flows = new[]
                {
                    new TmForgeFlowDto { Id = "33333333-3333-4333-8333-333333333333", Source = externalId, Target = processId, Name = "request" },
                },
            };
        }

        private static TmForgeModelDto ModelWithElements(int count, string prefix)
        {
            return new TmForgeModelDto
            {
                Elements = Enumerable.Range(0, count)
                    .Select(index => new TmForgeElementDto
                    {
                        Id = prefix + index,
                        Kind = "process",
                    })
                    .ToArray(),
            };
        }

        private static ServiceProvider CreateServices(
            string root,
            long maxReadBytes = McpPathPolicy.DefaultMaxReadBytes,
            long maxWriteBytes = McpPathPolicy.DefaultMaxWriteBytes)
        {
            return new ServiceCollection()
                .AddSingleton(new McpPathPolicy(root, maxReadBytes, maxWriteBytes))
                .BuildServiceProvider();
        }

        private static void CreateHardLink(string linkPath, string existingPath)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "ln",
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            if (OperatingSystem.IsWindows())
            {
                startInfo.ArgumentList.Add("/c");
                startInfo.ArgumentList.Add("mklink");
                startInfo.ArgumentList.Add("/H");
                startInfo.ArgumentList.Add(linkPath);
                startInfo.ArgumentList.Add(existingPath);
            }
            else
            {
                startInfo.ArgumentList.Add(existingPath);
                startInfo.ArgumentList.Add(linkPath);
            }

            Process? started = Process.Start(startInfo);
            using Process process = started!;
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.AreEqual(0, process.ExitCode, error);
        }

        private static void WriteZipEntry(ZipArchive archive, string name, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.SmallestSize);
            using StreamWriter writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }
    }
}
