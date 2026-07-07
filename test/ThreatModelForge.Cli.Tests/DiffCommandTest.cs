namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the <see cref="DiffCommand"/> class.
    /// </summary>
    [TestClass]
    public class DiffCommandTest
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
            this.WorkingDirectory = Path.Combine(Path.GetTempPath(), "tmforge-diff-" + Guid.NewGuid().ToString("N"));
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
        /// Verifies that two byte-identical models report no differences.
        /// </summary>
        [TestMethod]
        public void IdenticalModelsReportNoDifferences()
        {
            string basePath = this.Write("base.tm7", BuildBase(out _));
            string revisedPath = Path.Combine(this.WorkingDirectory, "revised.tm7");
            File.Copy(basePath, revisedPath);

            (int exit, string output) = Capture(new[] { basePath, revisedPath });

            Assert.AreEqual(0, exit);
            StringAssert.Contains(output, "No differences.");
        }

        /// <summary>
        /// Verifies that renaming an element is reported under the "Modified" section.
        /// </summary>
        [TestMethod]
        public void RenameIsReportedAsModified()
        {
            string basePath = this.Write("base.tm7", BuildBase(out Guid process));

            ThreatModel revised = Load(basePath);
            new DiagramEditor(revised).SetElementName(revised.DrawingSurfaceList[0], process, "Renamed");
            string revisedPath = this.Write("revised.tm7", revised);

            (int exit, string output) = Capture(new[] { basePath, revisedPath });

            Assert.AreEqual(0, exit);
            StringAssert.Contains(output, "Modified:");
            StringAssert.Contains(output, "Renamed");
        }

        /// <summary>
        /// Verifies that adding an element is counted in the JSON envelope summary.
        /// </summary>
        [TestMethod]
        public void AddedElementJsonReportsSummary()
        {
            string basePath = this.Write("base.tm7", BuildBase(out _));

            ThreatModel revised = Load(basePath);
            new DiagramEditor(revised).AddElement(revised.DrawingSurfaceList[0], StencilKind.Process, 10, 10);
            string revisedPath = this.Write("revised.tm7", revised);

            (int exit, string output) = Capture(new[] { basePath, revisedPath, "--json" });

            Assert.AreEqual(0, exit);
            using JsonDocument document = JsonDocument.Parse(output);
            JsonElement summary = document.RootElement.GetProperty("data").GetProperty("summary");
            Assert.AreEqual(1, summary.GetProperty("added").GetInt32());
            Assert.AreEqual(0, summary.GetProperty("removed").GetInt32());
        }

        /// <summary>
        /// Verifies that a missing input file is reported as an error.
        /// </summary>
        [TestMethod]
        public void MissingFileReturnsError()
        {
            string basePath = this.Write("base.tm7", BuildBase(out _));

            (int exit, _) = Capture(new[] { basePath, Path.Combine(this.WorkingDirectory, "does-not-exist.tm7") });

            Assert.AreEqual(1, exit);
        }

        private static (int Exit, string Output) Capture(string[] args)
        {
            StringWriter writer = new StringWriter();
            TextWriter original = Console.Out;
            Console.SetOut(writer);
            try
            {
                int exit = DiffCommand.Run(args);
                return (exit, writer.ToString());
            }
            finally
            {
                Console.SetOut(original);
            }
        }

        private static ThreatModel Load(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return ThreatModel.Load(stream);
        }

        private static ThreatModel BuildBase(out Guid process)
        {
            process = Guid.NewGuid();
            Guid store = Guid.NewGuid();
            Guid flow = Guid.NewGuid();

            StencilRectangle processElement = new StencilRectangle { Guid = process, TypeId = "GE.P", Left = 0, Top = 0, Width = 60, Height = 40 };
            DiagramElementHelper.SetName(processElement, "Web App");

            StencilEllipse storeElement = new StencilEllipse { Guid = store, TypeId = "GE.DS", Left = 200, Top = 0, Width = 60, Height = 40 };
            DiagramElementHelper.SetName(storeElement, "Database");

            Connector flowElement = new Connector { Guid = flow, TypeId = "GE.DF", SourceGuid = process, TargetGuid = store, SourceX = 60, SourceY = 20, TargetX = 200, TargetY = 20 };
            DiagramElementHelper.SetName(flowElement, "query");

            DrawingSurfaceModel surface = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "Main" };
            surface.Borders[process] = processElement;
            surface.Borders[store] = storeElement;
            surface.Lines[flow] = flowElement;

            ThreatModel model = new ThreatModel();
            model.DrawingSurfaceList.Add(surface);
            return model;
        }

        private string Write(string name, ThreatModel model)
        {
            string path = Path.Combine(this.WorkingDirectory, name);
            File.WriteAllBytes(path, new DiagramEditor(model).ToBytes());
            return path;
        }
    }
}
