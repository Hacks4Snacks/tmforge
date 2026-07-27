namespace ThreatModelForge.Analysis.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the <see cref="BoundaryCrossingDiff"/> class.
    /// </summary>
    [TestClass]
    public class BoundaryCrossingDiffTests
    {
        /// <summary>
        /// Gets or sets the test context.
        /// </summary>
        public TestContext? TestContext { get; set; }

        /// <summary>
        /// The reason this comparison exists at all: moving an element across a trust boundary changes
        /// no stored property, so the structural diff correctly sees nothing. If that ever stops being
        /// true this test should be revisited rather than deleted — but until then, the crossing
        /// comparison is the only thing standing between a reviewer and a silent change of exposure.
        /// </summary>
        [TestMethod]
        public void MovingAFlowOutOfABoundaryIsInvisibleToTheStructuralDiffButReportedAsACrossingChange()
        {
            ThreatModel baseModel = Build(out Guid flowId, out Guid boundaryId);
            ThreatModel revised = Build(out _, out _, sourceX: 10, sourceY: 10);

            Assert.IsTrue(
                ModelDiff.Compare(baseModel, revised).IsEmpty,
                "the structural diff is expected to see nothing here; that is the gap being covered");

            CrossingDifference crossings = BoundaryCrossingDiff.Compare(baseModel, revised);

            CrossingChange change = crossings.Changes.Single();
            Assert.AreEqual(flowId, change.FlowId);
            Assert.AreEqual(ChangeKind.Modified, change.Kind);
            Assert.AreEqual(0, change.Added.Count);
            Assert.AreEqual(boundaryId, change.Removed.Single().Id);
            Assert.AreEqual("Public Internet", change.Removed.Single().Name);
        }

        /// <summary>
        /// Verifies that a flow which starts crossing a boundary is reported as an addition, which is
        /// the direction a reviewer cares about most.
        /// </summary>
        [TestMethod]
        public void AFlowThatStartsCrossingIsReportedAsAdded()
        {
            ThreatModel baseModel = Build(out _, out Guid boundaryId, sourceX: 10, sourceY: 10);
            ThreatModel revised = Build(out _, out _);

            CrossingChange change = BoundaryCrossingDiff.Compare(baseModel, revised).Changes.Single();

            Assert.AreEqual(ChangeKind.Modified, change.Kind);
            Assert.AreEqual(boundaryId, change.Added.Single().Id);
            Assert.AreEqual(0, change.Removed.Count);
        }

        /// <summary>
        /// Verifies that renaming a boundary is not mistaken for the flow crossing a different one.
        /// Boundaries are matched by id precisely so that editorial changes stay quiet.
        /// </summary>
        [TestMethod]
        public void RenamingABoundaryIsNotACrossingChange()
        {
            ThreatModel baseModel = Build(out _, out _);
            ThreatModel revised = Build(out _, out _, boundaryName: "Renamed Boundary");

            Assert.IsTrue(BoundaryCrossingDiff.Compare(baseModel, revised).IsEmpty);
        }

        /// <summary>
        /// Verifies that a model compared against itself reports nothing.
        /// </summary>
        [TestMethod]
        public void AnUnchangedModelHasNoCrossingChanges()
        {
            ThreatModel baseModel = Build(out _, out _);
            ThreatModel revised = Build(out _, out _);

            Assert.IsTrue(BoundaryCrossingDiff.Compare(baseModel, revised).IsEmpty);
        }

        /// <summary>
        /// Verifies that a brand new flow which crosses a boundary is reported, and labelled as a new
        /// flow so the summary does not imply an existing flow changed.
        /// </summary>
        [TestMethod]
        public void ANewFlowThatCrossesABoundaryIsReportedAsAnAddedFlow()
        {
            ThreatModel baseModel = Build(out _, out Guid boundaryId);
            ThreatModel revised = Build(out _, out _);
            AddFlow(revised, Guid.NewGuid(), "Second flow", 60, 60, 500, 500);

            CrossingChange change = BoundaryCrossingDiff.Compare(baseModel, revised).Changes.Single();

            Assert.AreEqual(ChangeKind.Added, change.Kind);
            Assert.AreEqual("Second flow", change.FlowName);
            Assert.AreEqual(boundaryId, change.Added.Single().Id);
        }

        /// <summary>
        /// Verifies that deleting a flow which crossed a boundary is reported, so that removing
        /// exposure is as visible as adding it.
        /// </summary>
        [TestMethod]
        public void ADeletedFlowThatCrossedABoundaryIsReportedAsARemovedFlow()
        {
            ThreatModel baseModel = Build(out Guid flowId, out Guid boundaryId);
            ThreatModel revised = Build(out _, out _);
            revised.DrawingSurfaceList[0].Lines.Remove(flowId);

            CrossingChange change = BoundaryCrossingDiff.Compare(baseModel, revised).Changes.Single();

            Assert.AreEqual(ChangeKind.Removed, change.Kind);
            Assert.AreEqual(boundaryId, change.Removed.Single().Id);
        }

        /// <summary>
        /// Verifies that adding or removing a flow which never crossed anything produces no crossing
        /// change. The structural diff already reports it, and repeating it here would drown the
        /// crossings that carry the security signal.
        /// </summary>
        [TestMethod]
        public void AFlowThatCrossesNothingIsNotReported()
        {
            ThreatModel baseModel = Build(out _, out _);
            ThreatModel revised = Build(out _, out _);
            AddFlow(revised, Guid.NewGuid(), "Internal call", 500, 500, 520, 520);

            Assert.IsTrue(BoundaryCrossingDiff.Compare(baseModel, revised).IsEmpty);
        }

        /// <summary>
        /// Verifies that a flow which swaps one boundary for another reports both halves, rather than
        /// netting them out to nothing.
        /// </summary>
        [TestMethod]
        public void SwappingOneBoundaryForAnotherReportsBothHalves()
        {
            Guid second = Guid.NewGuid();
            ThreatModel baseModel = Build(out _, out Guid first);
            ThreatModel revised = Build(out _, out _);

            // Put a second boundary around the far endpoint in the revised model and move the near
            // endpoint out of the first, so exactly one crossing is traded for another.
            AddBoundary(revised, second, "Partner Network", 400, 400, 200, 200);
            Connector flow = revised.DrawingSurfaceList[0].Lines.Values.OfType<Connector>().Single();
            flow.SourceX = 450;
            flow.SourceY = 450;
            flow.TargetX = 800;
            flow.TargetY = 800;

            CrossingChange change = BoundaryCrossingDiff.Compare(baseModel, revised).Changes.Single();

            Assert.AreEqual(second, change.Added.Single().Id);
            Assert.AreEqual(first, change.Removed.Single().Id);
        }

        /// <summary>
        /// Verifies that <see cref="BoundaryCrossingDiff.Capture(ThreatModel)"/> records the diagram a
        /// flow belongs to, so a multi-diagram model reports crossings against the right page.
        /// </summary>
        [TestMethod]
        public void CaptureRecordsTheDiagramTheFlowIsDrawnOn()
        {
            ThreatModel model = Build(out Guid flowId, out _);

            FlowCrossings captured = BoundaryCrossingDiff.Capture(model).Single();

            Assert.AreEqual(flowId, captured.FlowId);
            Assert.AreEqual("DFD-0", captured.DiagramName);
            Assert.AreEqual("Public Internet", captured.Boundaries.Single().Name);
        }

        /// <summary>
        /// Builds a one-diagram model with a single boundary and a single flow that crosses it: the
        /// flow starts inside the boundary and ends outside it. The ids are deterministic so two calls
        /// produce comparable models. Passing a source outside the boundary produces the same model
        /// with no crossing.
        /// </summary>
        /// <param name="flowId">Receives the flow's id.</param>
        /// <param name="boundaryId">Receives the boundary's id.</param>
        /// <param name="sourceX">The flow's source x coordinate.</param>
        /// <param name="sourceY">The flow's source y coordinate.</param>
        /// <param name="boundaryName">The boundary's display name.</param>
        /// <returns>The model.</returns>
        private static ThreatModel Build(
            out Guid flowId,
            out Guid boundaryId,
            int sourceX = 60,
            int sourceY = 60,
            string boundaryName = "Public Internet")
        {
            flowId = new Guid("11111111-1111-1111-1111-111111111111");
            boundaryId = new Guid("22222222-2222-2222-2222-222222222222");

            ThreatModel model = new ThreatModel();
            model.DrawingSurfaceList.Add(new DrawingSurfaceModel
            {
                Guid = new Guid("33333333-3333-3333-3333-333333333333"),
                Header = "DFD-0",
            });

            AddBoundary(model, boundaryId, boundaryName, 50, 50, 200, 200);
            AddFlow(model, flowId, "Browse", sourceX, sourceY, 500, 500);
            return model;
        }

        private static void AddBoundary(ThreatModel model, Guid id, string name, int left, int top, int width, int height)
        {
            BorderBoundary boundary = new BorderBoundary
            {
                Guid = id,
                GenericTypeId = "GE.TB.B",
                TypeId = "GE.TB.B",
                Left = left,
                Top = top,
                Width = width,
                Height = height,
            };

            DiagramElementHelper.SetName(boundary, name);
            model.DrawingSurfaceList[0].Borders.Add(id, boundary);
        }

        private static void AddFlow(ThreatModel model, Guid id, string name, int sourceX, int sourceY, int targetX, int targetY)
        {
            Connector flow = new Connector
            {
                Guid = id,
                GenericTypeId = "GE.DF",
                TypeId = "GE.DF",
                SourceX = sourceX,
                SourceY = sourceY,
                TargetX = targetX,
                TargetY = targetY,
            };

            DiagramElementHelper.SetName(flow, name);
            model.DrawingSurfaceList[0].Lines.Add(id, flow);
        }
    }
}
