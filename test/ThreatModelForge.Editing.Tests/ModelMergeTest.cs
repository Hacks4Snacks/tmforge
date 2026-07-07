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
    /// Unit tests for <see cref="ModelMerge"/>.
    /// </summary>
    [TestClass]
    public class ModelMergeTest
    {
        /// <summary>
        /// Verifies that non-overlapping edits from both sides are combined without conflict.
        /// </summary>
        [TestMethod]
        public void CombinesNonOverlappingEdits()
        {
            (ThreatModel baseModel, Guid process, Guid store, _) = BuildBase();
            ThreatModel ours = Reload(baseModel);
            ThreatModel theirs = Reload(baseModel);
            Rename(ours, process, "API");
            Rename(theirs, store, "Warehouse");

            MergeResult result = ModelMerge.Merge(baseModel, ours, theirs);

            Assert.IsTrue(result.IsClean);
            Assert.AreEqual("API", NameOf(result.Merged, process));
            Assert.AreEqual("Warehouse", NameOf(result.Merged, store));
        }

        /// <summary>
        /// Verifies that elements added on each side are both present in the result.
        /// </summary>
        [TestMethod]
        public void UnionsAdditionsFromBothSides()
        {
            (ThreatModel baseModel, _, _, _) = BuildBase();
            ThreatModel ours = Reload(baseModel);
            ThreatModel theirs = Reload(baseModel);
            Guid mine = new DiagramEditor(ours).AddElement(ours.DrawingSurfaceList[0], StencilKind.Process, 10, 10);
            Guid incoming = new DiagramEditor(theirs).AddElement(theirs.DrawingSurfaceList[0], StencilKind.Process, 20, 20);

            MergeResult result = ModelMerge.Merge(baseModel, ours, theirs);

            Assert.IsTrue(result.IsClean);
            Assert.IsNotNull(DiagramEditor.FindElement(result.Merged.DrawingSurfaceList[0], mine));
            Assert.IsNotNull(DiagramEditor.FindElement(result.Merged.DrawingSurfaceList[0], incoming));
        }

        /// <summary>
        /// Verifies that a deletion made only by theirs is applied when ours left the element alone.
        /// </summary>
        [TestMethod]
        public void AppliesTheirsDeletion()
        {
            (ThreatModel baseModel, _, Guid store, Guid flow) = BuildBase();
            ThreatModel ours = Reload(baseModel);
            ThreatModel theirs = Reload(baseModel);
            new DiagramEditor(theirs).RemoveElement(theirs.DrawingSurfaceList[0], store);

            MergeResult result = ModelMerge.Merge(baseModel, ours, theirs);

            Assert.IsTrue(result.IsClean);
            Assert.IsFalse(result.Merged.DrawingSurfaceList[0].Borders.ContainsKey(store));
            Assert.IsFalse(result.Merged.DrawingSurfaceList[0].Lines.ContainsKey(flow));
        }

        /// <summary>
        /// Verifies that disjoint property edits on the same element are both applied.
        /// </summary>
        [TestMethod]
        public void CombinesDisjointPropertyEdits()
        {
            (ThreatModel baseModel, _, _, Guid flow) = BuildBase();
            ThreatModel ours = Reload(baseModel);
            ThreatModel theirs = Reload(baseModel);
            SetProperty(ours, flow, "Port", "443");
            SetProperty(theirs, flow, "DataType", "PII");

            MergeResult result = ModelMerge.Merge(baseModel, ours, theirs);

            Assert.IsTrue(result.IsClean);
            Entity? mergedFlow = DiagramEditor.FindElement(result.Merged.DrawingSurfaceList[0], flow);
            System.Collections.Generic.IReadOnlyDictionary<string, string> properties =
                DiagramElementHelper.GetCustomProperties(mergedFlow!);
            Assert.AreEqual("443", properties["Port"]);
            Assert.AreEqual("PII", properties["DataType"]);
        }

        /// <summary>
        /// Verifies that when both sides set the same property differently, ours is kept and the
        /// conflict is reported.
        /// </summary>
        [TestMethod]
        public void ReportsSamePropertyConflictAndKeepsOurs()
        {
            (ThreatModel baseModel, _, _, Guid flow) = BuildBase();
            ThreatModel ours = Reload(baseModel);
            ThreatModel theirs = Reload(baseModel);
            SetProperty(ours, flow, "Protocol", "HTTPS");
            SetProperty(theirs, flow, "Protocol", "mTLS");

            MergeResult result = ModelMerge.Merge(baseModel, ours, theirs);

            MergeConflict conflict = result.Conflicts.Single();
            Assert.AreEqual(MergeConflictKind.Property, conflict.Kind);
            Assert.AreEqual("Protocol", conflict.Property);
            Assert.AreEqual("HTTPS", conflict.Ours);
            Assert.AreEqual("mTLS", conflict.Theirs);
            Entity? mergedFlow = DiagramEditor.FindElement(result.Merged.DrawingSurfaceList[0], flow);
            Assert.AreEqual("HTTPS", DiagramElementHelper.GetCustomProperties(mergedFlow!)["Protocol"]);
        }

        /// <summary>
        /// Verifies that deleting on one side while modifying on the other is a conflict, and the
        /// modified (ours) element is kept.
        /// </summary>
        [TestMethod]
        public void ReportsDeleteModifyConflict()
        {
            (ThreatModel baseModel, Guid process, _, _) = BuildBase();
            ThreatModel ours = Reload(baseModel);
            ThreatModel theirs = Reload(baseModel);
            Rename(ours, process, "API");
            new DiagramEditor(theirs).RemoveElement(theirs.DrawingSurfaceList[0], process);

            MergeResult result = ModelMerge.Merge(baseModel, ours, theirs);

            MergeConflict conflict = result.Conflicts.Single();
            Assert.AreEqual(MergeConflictKind.DeleteModify, conflict.Kind);
            Assert.AreEqual(process, conflict.ElementId);
            Assert.AreEqual("API", NameOf(result.Merged, process));
        }

        /// <summary>
        /// Verifies that a flow left pointing at an element the merge removed is flagged as dangling.
        /// </summary>
        [TestMethod]
        public void DetectsDanglingReference()
        {
            (ThreatModel baseModel, _, Guid store, Guid flow) = BuildBase();
            ThreatModel ours = Reload(baseModel);
            ThreatModel theirs = Reload(baseModel);

            // Theirs removes the store but leaves the flow that points at it.
            theirs.DrawingSurfaceList[0].Borders.Remove(store);

            MergeResult result = ModelMerge.Merge(baseModel, ours, theirs);

            MergeConflict conflict = result.Conflicts.Single();
            Assert.AreEqual(MergeConflictKind.DanglingReference, conflict.Kind);
            Assert.AreEqual(flow, conflict.ElementId);
            Assert.AreEqual(ModelSnapshot.TargetKey, conflict.Property);
        }

        /// <summary>
        /// Verifies that merging three copies of the same model produces no changes and no conflicts.
        /// </summary>
        [TestMethod]
        public void IdenticalModelsMergeClean()
        {
            (ThreatModel baseModel, _, _, _) = BuildBase();
            ThreatModel ours = Reload(baseModel);
            ThreatModel theirs = Reload(baseModel);

            MergeResult result = ModelMerge.Merge(baseModel, ours, theirs);

            Assert.IsTrue(result.IsClean);
            Assert.AreSame(ours, result.Merged);
        }

        /// <summary>
        /// Verifies the two-way overload (no common ancestor): elements unique to either side are
        /// unioned, while a shared element whose attribute diverges is reported as a conflict and
        /// kept as <c>ours</c>, since without an ancestor neither side can be presumed authoritative.
        /// </summary>
        [TestMethod]
        public void TwoWayUnionsAddsAndConflictsOnSharedDivergence()
        {
            (ThreatModel baseModel, Guid process, _, _) = BuildBase();
            ThreatModel ours = Reload(baseModel);
            ThreatModel theirs = Reload(baseModel);
            Rename(ours, process, "Auth");
            Rename(theirs, process, "Frontend");
            Guid mine = new DiagramEditor(ours).AddElement(ours.DrawingSurfaceList[0], StencilKind.Process, 10, 10);
            Guid incoming = new DiagramEditor(theirs).AddElement(theirs.DrawingSurfaceList[0], StencilKind.Process, 20, 20);

            MergeResult result = ModelMerge.Merge(ours, theirs);

            MergeConflict conflict = result.Conflicts.Single();
            Assert.AreEqual(MergeConflictKind.Property, conflict.Kind);
            Assert.AreEqual(ModelSnapshot.NameKey, conflict.Property);
            Assert.IsNull(conflict.Base);
            Assert.AreEqual("Auth", conflict.Ours);
            Assert.AreEqual("Frontend", conflict.Theirs);
            Assert.AreEqual("Auth", NameOf(result.Merged, process));
            Assert.IsNotNull(DiagramEditor.FindElement(result.Merged.DrawingSurfaceList[0], mine));
            Assert.IsNotNull(DiagramEditor.FindElement(result.Merged.DrawingSurfaceList[0], incoming));
        }

        private static void Rename(ThreatModel model, Guid id, string name)
        {
            new DiagramEditor(model).SetElementName(model.DrawingSurfaceList[0], id, name);
        }

        private static void SetProperty(ThreatModel model, Guid id, string key, string value)
        {
            Entity? element = DiagramEditor.FindElement(model.DrawingSurfaceList[0], id);
            DiagramElementHelper.SetCustomProperty(element!, key, value);
        }

        private static string NameOf(ThreatModel model, Guid id)
        {
            Entity? element = DiagramEditor.FindElement(model.DrawingSurfaceList[0], id);
            return DiagramElementHelper.GetName(element!);
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
            return (model, process, store, flow);
        }
    }
}
