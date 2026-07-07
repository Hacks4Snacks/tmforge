namespace ThreatModelForge.Editing.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for <see cref="ModelDiff"/>.
    /// </summary>
    [TestClass]
    public class ModelDiffTest
    {
        /// <summary>
        /// Verifies that a model compared against its re-serialized copy reports no differences, so a
        /// mere round-trip never shows up as a change.
        /// </summary>
        [TestMethod]
        public void IdenticalModelsHaveNoDifferences()
        {
            (ThreatModel model, _, _, _) = BuildBase();
            ThreatModel copy = Reload(model);

            ModelDifference difference = ModelDiff.Compare(model, copy);

            Assert.IsTrue(difference.IsEmpty);
        }

        /// <summary>
        /// Verifies that an element present only in the revised model is reported as added.
        /// </summary>
        [TestMethod]
        public void AddedElementIsReported()
        {
            (ThreatModel model, _, _, _) = BuildBase();
            ThreatModel revised = Reload(model);
            Guid added = new DiagramEditor(revised).AddElement(revised.DrawingSurfaceList[0], StencilKind.Process, 10, 10);

            ModelDifference difference = ModelDiff.Compare(model, revised);

            Assert.AreEqual(0, difference.Removed.Count);
            Assert.AreEqual(0, difference.Modified.Count);
            ElementChange change = difference.Added.Single();
            Assert.AreEqual(added, change.Id);
            Assert.AreEqual(ChangeKind.Added, change.Kind);
            Assert.AreEqual("process", change.ElementKind);
        }

        /// <summary>
        /// Verifies that an element present only in the base model is reported as removed.
        /// </summary>
        [TestMethod]
        public void RemovedFlowIsReported()
        {
            (ThreatModel model, _, _, Guid flow) = BuildBase();
            ThreatModel revised = Reload(model);
            revised.DrawingSurfaceList[0].Lines.Remove(flow);

            ModelDifference difference = ModelDiff.Compare(model, revised);

            Assert.AreEqual(0, difference.Added.Count);
            ElementChange change = difference.Removed.Single();
            Assert.AreEqual(flow, change.Id);
            Assert.AreEqual("flow", change.ElementKind);
        }

        /// <summary>
        /// Verifies that renaming an element is reported as a single modification with a name change.
        /// </summary>
        [TestMethod]
        public void RenamedElementIsModified()
        {
            (ThreatModel model, Guid process, _, _) = BuildBase();
            ThreatModel revised = Reload(model);
            new DiagramEditor(revised).SetElementName(revised.DrawingSurfaceList[0], process, "Renamed");

            ModelDifference difference = ModelDiff.Compare(model, revised);

            Assert.AreEqual(0, difference.Added.Count);
            Assert.AreEqual(0, difference.Removed.Count);
            ElementChange change = difference.Modified.Single();
            Assert.AreEqual(process, change.Id);
            PropertyChange name = change.PropertyChanges.Single(property => property.Key == "name");
            Assert.AreEqual("Web App", name.From);
            Assert.AreEqual("Renamed", name.To);
        }

        /// <summary>
        /// Verifies that changing a custom property is reported as a modification of that property.
        /// </summary>
        [TestMethod]
        public void ChangedCustomPropertyIsModified()
        {
            (ThreatModel model, _, _, Guid flow) = BuildBase();
            ThreatModel revised = Reload(model);
            Entity? element = DiagramEditor.FindElement(revised.DrawingSurfaceList[0], flow);
            DiagramElementHelper.SetCustomProperty(element!, "Protocol", "HTTPS");

            ModelDifference difference = ModelDiff.Compare(model, revised);

            ElementChange change = difference.Modified.Single();
            Assert.AreEqual(flow, change.Id);
            PropertyChange protocol = change.PropertyChanges.Single(property => property.Key == "Protocol");
            Assert.AreEqual("HTTP", protocol.From);
            Assert.AreEqual("HTTPS", protocol.To);
        }

        /// <summary>
        /// Verifies that re-pointing a flow's endpoint is reported as a target change.
        /// </summary>
        [TestMethod]
        public void ReroutedFlowIsModified()
        {
            (ThreatModel model, _, _, Guid flow) = BuildBase();
            ThreatModel revised = Reload(model);
            LineElement line = (LineElement)revised.DrawingSurfaceList[0].Lines[flow];
            Guid newTarget = Guid.NewGuid();
            line.TargetGuid = newTarget;

            ModelDifference difference = ModelDiff.Compare(model, revised);

            ElementChange change = difference.Modified.Single(candidate => candidate.Id == flow);
            PropertyChange target = change.PropertyChanges.Single(property => property.Key == "target");
            Assert.AreEqual(newTarget.ToString(), target.To);
        }

        private static ThreatModel Reload(ThreatModel model)
        {
            byte[] bytes = new DiagramEditor(model).ToBytes();
            using MemoryStream stream = new MemoryStream(bytes);
            return ThreatModel.Load(stream);
        }

        private static (ThreatModel Model, Guid Process, Guid Store, Guid Flow) BuildBase()
        {
            Guid process = Guid.NewGuid();
            Guid store = Guid.NewGuid();
            Guid flow = Guid.NewGuid();

            StencilRectangle processElement = new StencilRectangle { Guid = process, TypeId = "GE.P", Left = 0, Top = 0, Width = 60, Height = 40 };
            DiagramElementHelper.SetName(processElement, "Web App");

            StencilEllipse storeElement = new StencilEllipse { Guid = store, TypeId = "GE.DS", Left = 200, Top = 0, Width = 60, Height = 40 };
            DiagramElementHelper.SetName(storeElement, "Database");

            Connector flowElement = new Connector { Guid = flow, TypeId = "GE.DF", SourceGuid = process, TargetGuid = store, SourceX = 60, SourceY = 20, TargetX = 200, TargetY = 20 };
            DiagramElementHelper.SetName(flowElement, "query");
            DiagramElementHelper.SetCustomProperty(flowElement, "Protocol", "HTTP");

            DrawingSurfaceModel surface = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "Main" };
            surface.Borders[process] = processElement;
            surface.Borders[store] = storeElement;
            surface.Lines[flow] = flowElement;

            ThreatModel model = new ThreatModel();
            model.DrawingSurfaceList.Add(surface);
            return (model, process, store, flow);
        }
    }
}
