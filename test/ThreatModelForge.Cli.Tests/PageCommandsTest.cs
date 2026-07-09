namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for the multi-page CLI surface: the <c>page</c> command (ls/add/rename/rm/reorder)
    /// and the <c>--page</c> selector on the authoring verbs.
    /// </summary>
    [TestClass]
    public class PageCommandsTest
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
            this.WorkingDirectory = Path.Join(Path.GetTempPath(), "tmforge-pages-" + Guid.NewGuid().ToString("N"));
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
        /// Verifies that <c>page add</c> appends a named page after the default first page.
        /// </summary>
        [TestMethod]
        public void PageAddAppendsNamedPage()
        {
            string path = this.NewModel();

            (int exit, _) = Capture(() => PageCommand.Run(new[] { "add", path, "--name", "Payments" }));

            Assert.AreEqual(0, exit);
            JsonElement data = PageList(path);
            Assert.AreEqual(2, data.GetProperty("count").GetInt32());
            Assert.AreEqual("Diagram 1", data.GetProperty("items")[0].GetProperty("name").GetString());
            Assert.AreEqual("Payments", data.GetProperty("items")[1].GetProperty("name").GetString());
        }

        /// <summary>
        /// Verifies that <c>page add</c> with no name defaults to "Diagram N".
        /// </summary>
        [TestMethod]
        public void PageAddDefaultsName()
        {
            string path = this.NewModel();

            (int exit, _) = Capture(() => PageCommand.Run(new[] { "add", path }));

            Assert.AreEqual(0, exit);
            Assert.AreEqual("Diagram 2", PageList(path).GetProperty("items")[1].GetProperty("name").GetString());
        }

        /// <summary>
        /// Verifies that <c>page rename</c> changes a page's name, selected by index.
        /// </summary>
        [TestMethod]
        public void PageRenameChangesName()
        {
            string path = this.NewModel();

            (int exit, _) = Capture(() => PageCommand.Run(new[] { "rename", path, "--page", "1", "--name", "Context" }));

            Assert.AreEqual(0, exit);
            Assert.AreEqual("Context", PageList(path).GetProperty("items")[0].GetProperty("name").GetString());
        }

        /// <summary>
        /// Verifies that <c>page rm</c> deletes the page selected by name.
        /// </summary>
        [TestMethod]
        public void PageRemoveDeletesSelectedPage()
        {
            string path = this.NewModel();
            Capture(() => PageCommand.Run(new[] { "add", path, "--name", "Payments" }));

            (int exit, _) = Capture(() => PageCommand.Run(new[] { "rm", path, "--page", "Payments" }));

            Assert.AreEqual(0, exit);
            JsonElement data = PageList(path);
            Assert.AreEqual(1, data.GetProperty("count").GetInt32());
            Assert.AreEqual("Diagram 1", data.GetProperty("items")[0].GetProperty("name").GetString());
        }

        /// <summary>
        /// Verifies that <c>page rm</c> refuses to delete the only page.
        /// </summary>
        [TestMethod]
        public void PageRemoveRefusesLastPage()
        {
            string path = this.NewModel();

            (int exit, _) = Capture(() => PageCommand.Run(new[] { "rm", path, "--page", "1" }));

            Assert.AreEqual(1, exit);
            Assert.AreEqual(1, PageList(path).GetProperty("count").GetInt32());
        }

        /// <summary>
        /// Verifies that <c>page reorder</c> moves a page (selected by name) to a 1-based position.
        /// </summary>
        [TestMethod]
        public void PageReorderMovesPage()
        {
            string path = this.NewModel();
            Capture(() => PageCommand.Run(new[] { "add", path, "--name", "Two" }));
            Capture(() => PageCommand.Run(new[] { "add", path, "--name", "Three" }));

            (int exit, _) = Capture(() => PageCommand.Run(new[] { "reorder", path, "--page", "Three", "--to", "1" }));

            Assert.AreEqual(0, exit);
            JsonElement items = PageList(path).GetProperty("items");
            Assert.AreEqual("Three", items[0].GetProperty("name").GetString());
            Assert.AreEqual("Diagram 1", items[1].GetProperty("name").GetString());
            Assert.AreEqual("Two", items[2].GetProperty("name").GetString());
        }

        /// <summary>
        /// Verifies that <c>add --page</c> places a new element on the page named by the selector.
        /// </summary>
        [TestMethod]
        public void AddPlacesElementOnNamedPage()
        {
            string path = this.NewModel();
            Capture(() => PageCommand.Run(new[] { "add", path, "--name", "Payments" }));

            (int exit, _) = Capture(() => AddCommand.Run(new[] { "process", path, "--name", "Ledger Svc", "--page", "Payments" }));

            Assert.AreEqual(0, exit);
            JsonElement items = PageList(path).GetProperty("items");
            Assert.AreEqual(0, items[0].GetProperty("components").GetInt32());
            Assert.AreEqual(1, items[1].GetProperty("components").GetInt32());
        }

        /// <summary>
        /// Verifies that <c>add --page</c> also accepts a 1-based page index.
        /// </summary>
        [TestMethod]
        public void AddPlacesElementOnPageByIndex()
        {
            string path = this.NewModel();
            Capture(() => PageCommand.Run(new[] { "add", path }));

            (int exit, _) = Capture(() => AddCommand.Run(new[] { "process", path, "--name", "X", "--page", "2" }));

            Assert.AreEqual(0, exit);
            Assert.AreEqual(1, PageList(path).GetProperty("items")[1].GetProperty("components").GetInt32());
        }

        /// <summary>
        /// Verifies that <c>add --page</c> with an out-of-range page is reported as an error.
        /// </summary>
        [TestMethod]
        public void AddUnknownPageReturnsError()
        {
            string path = this.NewModel();

            (int exit, _) = Capture(() => AddCommand.Run(new[] { "process", path, "--name", "X", "--page", "99" }));

            Assert.AreEqual(1, exit);
        }

        /// <summary>
        /// Verifies that <c>rename</c> (without <c>--page</c>) finds an element on any page.
        /// </summary>
        [TestMethod]
        public void RenameFindsElementOnAnyPage()
        {
            string path = this.NewModel();
            Capture(() => PageCommand.Run(new[] { "add", path, "--name", "Payments" }));
            string id = AddOnPage(path, "process", "Original", "Payments");

            (int exit, _) = Capture(() => RenameCommand.Run(new[] { path, "--id", id, "--name", "Renamed" }));

            Assert.AreEqual(0, exit);
            (int listExit, string listOut) = Capture(() => ListCommand.Run(new[] { "components", "--json", path }));
            Assert.AreEqual(0, listExit);
            JsonElement items = JsonDocument.Parse(listOut).RootElement.GetProperty("data").GetProperty("items");
            Assert.AreEqual("Renamed", items[0].GetProperty("name").GetString());
        }

        /// <summary>
        /// Verifies that <c>remove</c> (without <c>--page</c>) removes an element from any page.
        /// </summary>
        [TestMethod]
        public void RemoveFindsElementOnAnyPage()
        {
            string path = this.NewModel();
            Capture(() => PageCommand.Run(new[] { "add", path, "--name", "Payments" }));
            string id = AddOnPage(path, "process", "Doomed", "Payments");

            (int exit, _) = Capture(() => RemoveCommand.Run(new[] { path, "--id", id }));

            Assert.AreEqual(0, exit);
            Assert.AreEqual(0, PageList(path).GetProperty("items")[1].GetProperty("components").GetInt32());
        }

        private static JsonElement PageList(string path)
        {
            (int exit, string stdout) = Capture(() => PageCommand.Run(new[] { "ls", "--json", path }));
            Assert.AreEqual(0, exit);
            using JsonDocument document = JsonDocument.Parse(stdout);
            return document.RootElement.GetProperty("data").Clone();
        }

        private static string AddOnPage(string path, string kind, string name, string page)
        {
            (int exit, string stdout) = Capture(() => AddCommand.Run(new[] { kind, path, "--name", name, "--page", page, "--json" }));
            Assert.AreEqual(0, exit);
            using JsonDocument document = JsonDocument.Parse(stdout);
            return document.RootElement.GetProperty("data").GetProperty("id").GetString() ?? string.Empty;
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
