namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for the <see cref="MergeCommand"/> class.
    /// </summary>
    [TestClass]
    public class MergeCommandTest
    {
        /// <summary>
        /// Gets or sets the working directory created for each test.
        /// </summary>
        private string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Creates an isolated working directory for the test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            this.WorkingDirectory = Path.Combine(Path.GetTempPath(), "tmforge-merge-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.WorkingDirectory);
        }

        /// <summary>
        /// Removes the working directory after the test.
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(this.WorkingDirectory))
            {
                Directory.Delete(this.WorkingDirectory, recursive: true);
            }
        }

        /// <summary>
        /// Verifies that a clean, non-overlapping merge returns zero and applies both sides' edits.
        /// </summary>
        [TestMethod]
        public void CleanMergeAppliesBothSidesAndReturnsZero()
        {
            string basePath = this.Write("base.tm7", BuildBase(out Guid process, out Guid store, out _));
            string oursPath = this.Write("ours.tm7", Rename(this.Load(basePath), process, "API"));
            string theirsPath = this.Write("theirs.tm7", Rename(this.Load(basePath), store, "Warehouse"));
            string mergedPath = Path.Combine(this.WorkingDirectory, "merged.tm7");

            (int exit, _) = Capture(new[] { basePath, oursPath, theirsPath, "--output", mergedPath });

            Assert.AreEqual(0, exit);
            ThreatModel merged = this.Load(mergedPath);
            Assert.AreEqual("API", NameOf(merged, process));
            Assert.AreEqual("Warehouse", NameOf(merged, store));
        }

        /// <summary>
        /// Verifies that a conflicting merge returns one and reports the conflict in the JSON envelope.
        /// </summary>
        [TestMethod]
        public void ConflictReturnsOneAndReportsInJson()
        {
            string basePath = this.Write("base.tm7", BuildBase(out _, out _, out Guid flow));
            string oursPath = this.Write("ours.tm7", SetProperty(this.Load(basePath), flow, "Protocol", "HTTPS"));
            string theirsPath = this.Write("theirs.tm7", SetProperty(this.Load(basePath), flow, "Protocol", "mTLS"));
            string mergedPath = Path.Combine(this.WorkingDirectory, "merged.tm7");

            (int exit, string output) = Capture(new[] { basePath, oursPath, theirsPath, "--output", mergedPath, "--json" });

            Assert.AreEqual(1, exit);
            using JsonDocument document = JsonDocument.Parse(output);
            JsonElement data = document.RootElement.GetProperty("data");
            Assert.AreEqual("conflict", data.GetProperty("status").GetString());
            Assert.AreEqual(1, data.GetProperty("summary").GetProperty("conflicts").GetInt32());
            Assert.AreEqual("HTTPS", PropertyOf(this.Load(mergedPath), flow, "Protocol"));
        }

        /// <summary>
        /// Verifies that a conflicting merge writes the conflict sidecar next to the output.
        /// </summary>
        [TestMethod]
        public void ConflictWritesSidecar()
        {
            string basePath = this.Write("base.tm7", BuildBase(out _, out _, out Guid flow));
            string oursPath = this.Write("ours.tm7", SetProperty(this.Load(basePath), flow, "Protocol", "HTTPS"));
            string theirsPath = this.Write("theirs.tm7", SetProperty(this.Load(basePath), flow, "Protocol", "mTLS"));
            string mergedPath = Path.Combine(this.WorkingDirectory, "merged.tm7");

            (int exit, _) = Capture(new[] { basePath, oursPath, theirsPath, "--output", mergedPath });

            Assert.AreEqual(1, exit);
            Assert.IsTrue(File.Exists(mergedPath + ".conflicts.json"));
        }

        /// <summary>
        /// Verifies that a missing input file is reported as an error.
        /// </summary>
        [TestMethod]
        public void MissingFileReturnsError()
        {
            string basePath = this.Write("base.tm7", BuildBase(out _, out _, out _));

            (int exit, _) = Capture(new[] { basePath, basePath, Path.Combine(this.WorkingDirectory, "missing.tm7") });

            Assert.AreEqual(1, exit);
        }

        private static (int Exit, string Output) Capture(string[] args)
        {
            StringWriter writer = new StringWriter();
            TextWriter original = Console.Out;
            Console.SetOut(writer);
            try
            {
                int exit = MergeCommand.Run(args);
                return (exit, writer.ToString());
            }
            finally
            {
                Console.SetOut(original);
            }
        }

        private static ThreatModel Rename(ThreatModel model, Guid id, string name)
        {
            new DiagramEditor(model).SetElementName(model.DrawingSurfaceList[0], id, name);
            return model;
        }

        private static ThreatModel SetProperty(ThreatModel model, Guid id, string key, string value)
        {
            Entity? element = DiagramEditor.FindElement(model.DrawingSurfaceList[0], id);
            DiagramElementHelper.SetCustomProperty(element!, key, value);
            return model;
        }

        private static string NameOf(ThreatModel model, Guid id)
        {
            Entity? element = DiagramEditor.FindElement(model.DrawingSurfaceList[0], id);
            return DiagramElementHelper.GetName(element!);
        }

        private static string PropertyOf(ThreatModel model, Guid id, string key)
        {
            Entity? element = DiagramEditor.FindElement(model.DrawingSurfaceList[0], id);
            return DiagramElementHelper.GetCustomProperties(element!)[key];
        }

        private static ThreatModel BuildBase(out Guid process, out Guid store, out Guid flow)
        {
            process = Guid.NewGuid();
            store = Guid.NewGuid();
            flow = Guid.NewGuid();

            StencilRectangle processElement = new StencilRectangle { Guid = process, TypeId = "GE.P" };
            DiagramElementHelper.SetName(processElement, "Web App");

            StencilEllipse storeElement = new StencilEllipse { Guid = store, TypeId = "GE.DS" };
            DiagramElementHelper.SetName(storeElement, "Database");

            Connector flowElement = new Connector { Guid = flow, TypeId = "GE.DF", SourceGuid = process, TargetGuid = store };
            DiagramElementHelper.SetName(flowElement, "query");
            DiagramElementHelper.SetCustomProperty(flowElement, "Protocol", "HTTP");

            DrawingSurfaceModel surface = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "Main" };
            surface.Borders[process] = processElement;
            surface.Borders[store] = storeElement;
            surface.Lines[flow] = flowElement;

            ThreatModel model = new ThreatModel();
            model.DrawingSurfaceList.Add(surface);
            return model;
        }

        private ThreatModel Load(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return ThreatModel.Load(stream);
        }

        private string Write(string name, ThreatModel model)
        {
            string path = Path.Combine(this.WorkingDirectory, name);
            File.WriteAllBytes(path, new DiagramEditor(model).ToBytes());
            return path;
        }
    }
}
