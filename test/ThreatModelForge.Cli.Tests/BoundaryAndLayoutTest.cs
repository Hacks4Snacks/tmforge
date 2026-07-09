namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for logical boundary membership (<c>add --boundary</c>) and the auto-layout verb
    /// (<c>tmforge layout</c>).
    /// </summary>
    [TestClass]
    public class BoundaryAndLayoutTest
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
            this.WorkingDirectory = Path.Join(Path.GetTempPath(), "tmforge-boundary-" + Guid.NewGuid().ToString("N"));
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
        /// <c>add --boundary</c> records membership and positions the element inside the boundary.
        /// </summary>
        [TestMethod]
        public void AddBoundaryRecordsMembershipAndPlacesInside()
        {
            string path = this.NewModel();
            Capture(() => AddCommand.Run(new[] { "boundary", path, "--alias", "TB", "--name", "Edge" }));

            (int exit, _) = Capture(() => AddCommand.Run(new[] { "process", path, "--alias", "P1", "--name", "Proc", "--boundary", "TB" }));
            Assert.AreEqual(0, exit);

            (int showExit, string showOut) = Capture(() => ShowCommand.Run(new[] { path, "--id", "P1", "--json" }));
            Assert.AreEqual(0, showExit);
            using JsonDocument document = JsonDocument.Parse(showOut);
            Assert.AreEqual("TB", document.RootElement.GetProperty("data").GetProperty("properties").GetProperty("Boundary").GetString());

            (DrawingElement boundary, DrawingElement process) = LoadPair(path, "Edge", "Proc");
            Assert.IsTrue(process.Left >= boundary.Left && process.Left < boundary.Left + boundary.Width, "the element must be within the boundary horizontally");
            Assert.IsTrue(process.Top >= boundary.Top && process.Top < boundary.Top + boundary.Height, "the element must be within the boundary vertically");
        }

        /// <summary>
        /// <c>add --boundary</c> with an unknown boundary reference is rejected.
        /// </summary>
        [TestMethod]
        public void AddBoundaryUnknownReferenceFails()
        {
            string path = this.NewModel();

            (int exit, _) = Capture(() => AddCommand.Run(new[] { "process", path, "--name", "Proc", "--boundary", "does-not-exist" }));

            Assert.AreEqual(1, exit);
        }

        /// <summary>
        /// <c>tmforge layout</c> arranges the components and reports how many it placed.
        /// </summary>
        [TestMethod]
        public void LayoutArrangesComponents()
        {
            string path = this.NewModel();
            string a = AddElement("process", path, "A");
            string b = AddElement("process", path, "B");
            Capture(() => ConnectCommand.Run(new[] { path, "--source", a, "--target", b }));

            (int exit, string stdout) = Capture(() => LayoutCommand.Run(new[] { path, "--json" }));

            Assert.AreEqual(0, exit);
            using JsonDocument document = JsonDocument.Parse(stdout);
            Assert.AreEqual(2, document.RootElement.GetProperty("data").GetProperty("components").GetInt32());

            (DrawingElement first, DrawingElement second) = LoadPair(path, "A", "B");
            Assert.AreNotEqual(first.Left, second.Left, "connected components should land in different layers (columns)");
        }

        private static string AddElement(string kind, string path, string name)
        {
            (int exit, string stdout) = Capture(() => AddCommand.Run(new[] { kind, path, "--name", name, "--json" }));
            Assert.AreEqual(0, exit);
            using JsonDocument document = JsonDocument.Parse(stdout);
            return document.RootElement.GetProperty("data").GetProperty("id").GetString() ?? string.Empty;
        }

        private static (DrawingElement First, DrawingElement Second) LoadPair(string path, string firstName, string secondName)
        {
            (ThreatModel model, _) = CliModelLoader.Load(path);
            DrawingSurfaceModel diagram = model.DrawingSurfaceList[0];
            DrawingElement first = FindByName(diagram, firstName);
            DrawingElement second = FindByName(diagram, secondName);
            return (first, second);
        }

        private static DrawingElement FindByName(DrawingSurfaceModel diagram, string name)
        {
            foreach (DrawingElement element in diagram.Borders.Values.OfType<DrawingElement>()
                .Where(element => string.Equals(DiagramElementHelper.GetName(element), name, StringComparison.Ordinal)))
            {
                return element;
            }

            throw new InvalidOperationException("Element not found: " + name);
        }

        private static (int Exit, string Stdout) Capture(Func<int> run)
        {
            using StringWriter outWriter = new StringWriter();
            using StringWriter errorWriter = new StringWriter();
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);
            try
            {
                int exit = run();
                return (exit, outWriter.ToString());
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }
        }

        private string NewModel()
        {
            string path = Path.Join(this.WorkingDirectory, "model.tm7");
            Capture(() => NewCommand.Run(new[] { path, "--name", "Test" }));
            return path;
        }
    }
}
