namespace ThreatModelForge.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for the <see cref="ThreatModel"/> class.
    /// </summary>
    [TestClass]
    [DeploymentItem("SampleModel.tm7")]
    public class ThreatModelTests
    {
        /// <summary>
        /// Gets or sets the test context.
        /// </summary>
        public TestContext? TestContext { get; set; }

        /// <summary>
        /// Unit test for the <see cref="ThreatModel.Load(string)"/> method.
        /// </summary>
        [TestMethod]
        public void LoadTest()
        {
            Assert.IsNotNull(this.TestContext!.DeploymentDirectory);
            string path = Path.Join(this.TestContext!.DeploymentDirectory, "SampleModel.tm7");

            ThreatModel actual = ThreatModel.Load(path);
            Assert.IsNotNull(actual);
            Assert.AreEqual(1, actual.DrawingSurfaceList.Count);

            DrawingSurfaceModel model = actual.DrawingSurfaceList[0];
            Assert.IsNotNull(model);
            Assert.AreEqual("DRAWINGSURFACE", model.GenericTypeId);
            Assert.AreEqual(new Guid("5a3e9d10-0000-4a00-9000-00000000d1a6"), model.Guid);
            Assert.AreEqual("DRAWINGSURFACE", model.TypeId);
            Assert.AreEqual(2, model.Properties.Count);
            Assert.IsTrue(model.Properties.OfType<HeaderDisplayAttribute>().Any(e => string.Equals(e.DisplayName, "Diagram", StringComparison.Ordinal)));
            Assert.IsTrue(model.Properties.OfType<StringDisplayAttribute>().Any(e => object.Equals(e.Value, "Sample")));

            Assert.AreEqual("Sample", model.Header);
            Assert.AreEqual(0.75f, model.Zoom);

            Assert.AreEqual(6, model.Borders.Count);

            Assert.AreEqual(5, model.Lines.Count);
        }

        /// <summary>
        /// Unit test for the <see cref="ThreatModel.Save(string)"/> method.
        /// </summary>
        [TestMethod]
        public void SaveTest()
        {
            Assert.IsNotNull(this.TestContext!.DeploymentDirectory);
            string path = Path.Join(
                this.TestContext!.DeploymentDirectory,
                $"{Guid.NewGuid()}.tm7");
            ThreatModel target = new ThreatModel();
            DrawingSurfaceModel model = new DrawingSurfaceModel
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = "DRAWINGSURFACE",
                Properties =
                {
                    new HeaderDisplayAttribute { DisplayName = "Diagram" },
                    new StringDisplayAttribute { DisplayName = "Name", Value = "MSI" },
                },
            };

            target.DrawingSurfaceList.Add(model);
            target.Save(path);
            Assert.IsTrue(File.Exists(path));
        }

        /// <summary>
        /// Round-trip fidelity test for <see cref="ThreatModel.Load(string)"/> and
        /// <see cref="ThreatModel.Save(string)"/>. Loads an engine-authored TM7 fixture,
        /// saves it, reloads it, and saves it again. Verifies that the cross-platform engine
        /// reads and writes .tm7 losslessly: re-serialization is byte-stable and the semantic
        /// content of the source model is preserved across the round trip.
        /// </summary>
        [TestMethod]
        public void RoundTripFidelityTest()
        {
            Assert.IsNotNull(this.TestContext!.DeploymentDirectory);
            string sourcePath = Path.Join(this.TestContext!.DeploymentDirectory, "SampleModel.tm7");

            // Load an engine-authored threat model fixture.
            ThreatModel original = ThreatModel.Load(sourcePath);

            // First round trip: save the loaded model, then reload it.
            string pathA = Path.Join(this.TestContext!.DeploymentDirectory, $"{Guid.NewGuid()}.tm7");
            original.Save(pathA);
            ThreatModel reloaded = ThreatModel.Load(pathA);

            // Second round trip: save the reloaded model again.
            string pathB = Path.Join(this.TestContext!.DeploymentDirectory, $"{Guid.NewGuid()}.tm7");
            reloaded.Save(pathB);

            try
            {
                // Once canonicalized by the engine, re-serialization must be byte-for-byte
                // identical, proving a lossless round trip through load and save.
                byte[] bytesA = File.ReadAllBytes(pathA);
                byte[] bytesB = File.ReadAllBytes(pathB);
                Assert.IsTrue(
                    bytesA.SequenceEqual(bytesB),
                    "Re-serializing a reloaded model must produce byte-identical output.");

                // The known structure of the source model survives the round trip.
                Assert.AreEqual(original.DrawingSurfaceList.Count, reloaded.DrawingSurfaceList.Count);
                Assert.AreEqual(1, reloaded.DrawingSurfaceList.Count);

                DrawingSurfaceModel originalDiagram = original.DrawingSurfaceList[0];
                DrawingSurfaceModel reloadedDiagram = reloaded.DrawingSurfaceList[0];

                Assert.AreEqual(originalDiagram.Guid, reloadedDiagram.Guid);
                Assert.AreEqual("Sample", reloadedDiagram.Header);
                Assert.AreEqual(originalDiagram.Header, reloadedDiagram.Header);
                Assert.AreEqual(0.75f, reloadedDiagram.Zoom);
                Assert.AreEqual(originalDiagram.Zoom, reloadedDiagram.Zoom);

                Assert.AreEqual(6, reloadedDiagram.Borders.Count);
                Assert.AreEqual(originalDiagram.Borders.Count, reloadedDiagram.Borders.Count);

                Assert.AreEqual(5, reloadedDiagram.Lines.Count);
                Assert.AreEqual(originalDiagram.Lines.Count, reloadedDiagram.Lines.Count);

                // Threats, notes, and the format version are preserved across the round trip.
                Assert.AreEqual(original.AllThreatsDictionary.Count, reloaded.AllThreatsDictionary.Count);
                Assert.AreEqual(original.Notes.Count, reloaded.Notes.Count);
                Assert.AreEqual(original.Version, reloaded.Version);
            }
            finally
            {
                File.Delete(pathA);
                File.Delete(pathB);
            }
        }
    }
}
