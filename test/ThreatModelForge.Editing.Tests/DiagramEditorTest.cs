namespace ThreatModelForge.Editing.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Editing;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for <see cref="DiagramEditor"/>.
    /// </summary>
    [TestClass]
    public class DiagramEditorTest
    {
        private const string NodeName = "Web App";

        /// <summary>
        /// Verifies that moving a component also moves the endpoints of connectors attached to it.
        /// </summary>
        [TestMethod]
        public void MoveComponentMovesAttachedConnector()
        {
            (ThreatModel model, Guid node, _, Guid connector) = Build();
            DiagramEditor editor = new DiagramEditor(model);
            DrawingSurfaceModel diagram = editor.Model.DrawingSurfaceList[0];

            editor.BeginChange();
            editor.MoveElementBy(diagram, node, 10, 20);

            Assert.AreEqual(10, Component(diagram, node).Left);
            Assert.AreEqual(20, Component(diagram, node).Top);
            Assert.AreEqual(50, Line(diagram, connector).SourceX);
            Assert.AreEqual(40, Line(diagram, connector).SourceY);
            Assert.AreEqual(200, Line(diagram, connector).TargetX);
        }

        /// <summary>
        /// Verifies that moving a connector shifts both of its endpoints.
        /// </summary>
        [TestMethod]
        public void MoveLineMovesBothEndpoints()
        {
            (ThreatModel model, _, _, Guid connector) = Build();
            DiagramEditor editor = new DiagramEditor(model);
            DrawingSurfaceModel diagram = editor.Model.DrawingSurfaceList[0];

            editor.MoveElementBy(diagram, connector, 5, 7);

            Assert.AreEqual(45, Line(diagram, connector).SourceX);
            Assert.AreEqual(27, Line(diagram, connector).SourceY);
            Assert.AreEqual(205, Line(diagram, connector).TargetX);
            Assert.AreEqual(37, Line(diagram, connector).TargetY);
        }

        /// <summary>
        /// Verifies that renaming updates the existing name property.
        /// </summary>
        [TestMethod]
        public void SetElementNameUpdatesExistingName()
        {
            (ThreatModel model, Guid node, _, _) = Build();
            DiagramEditor editor = new DiagramEditor(model);
            DrawingSurfaceModel diagram = editor.Model.DrawingSurfaceList[0];

            editor.SetElementName(diagram, node, "Renamed");

            Entity? renamed = DiagramEditor.FindElement(diagram, node);
            Assert.AreEqual("Renamed", DiagramElementHelper.GetName(renamed!));
        }

        /// <summary>
        /// Verifies that removing a component also removes any connectors attached to it.
        /// </summary>
        [TestMethod]
        public void RemoveComponentAlsoRemovesConnectors()
        {
            (ThreatModel model, Guid node, Guid other, Guid connector) = Build();
            DiagramEditor editor = new DiagramEditor(model);
            DrawingSurfaceModel diagram = editor.Model.DrawingSurfaceList[0];

            editor.RemoveElement(diagram, node);

            Assert.IsFalse(diagram.Borders.ContainsKey(node));
            Assert.IsFalse(diagram.Lines.ContainsKey(connector));
            Assert.IsTrue(diagram.Borders.ContainsKey(other));
        }

        /// <summary>
        /// Verifies that adding a process creates an ellipse with the generic type id and a name.
        /// </summary>
        [TestMethod]
        public void AddElementCreatesNamedTypedComponent()
        {
            (ThreatModel model, _, _, _) = Build();
            DiagramEditor editor = new DiagramEditor(model);
            DrawingSurfaceModel diagram = editor.Model.DrawingSurfaceList[0];
            int before = diagram.Borders.Count;

            Guid guid = editor.AddElement(diagram, StencilKind.Process, 50, 60);

            Assert.AreEqual(before + 1, diagram.Borders.Count);
            Entity? element = DiagramEditor.FindElement(diagram, guid);
            Assert.IsInstanceOfType(element, typeof(StencilEllipse));
            Assert.AreEqual("GE.P", ((StencilEllipse)element!).TypeId);
            Assert.AreEqual("GE.P", ((StencilEllipse)element).GenericTypeId);
            Assert.AreEqual(50, ((StencilEllipse)element).Left);
            Assert.AreEqual("Process", DiagramElementHelper.GetName(element));
        }

        /// <summary>
        /// Verifies that adding a connector links two components with endpoints at their centers.
        /// </summary>
        [TestMethod]
        public void AddConnectorLinksComponentsAtCenters()
        {
            (ThreatModel model, Guid node, Guid other, _) = Build();
            DiagramEditor editor = new DiagramEditor(model);
            DrawingSurfaceModel diagram = editor.Model.DrawingSurfaceList[0];
            int before = diagram.Lines.Count;

            Guid guid = editor.AddConnector(diagram, node, other);

            Assert.AreEqual(before + 1, diagram.Lines.Count);
            LineElement line = (LineElement)diagram.Lines[guid];
            Assert.AreEqual(node, line.SourceGuid);
            Assert.AreEqual(other, line.TargetGuid);

            // Endpoints attach to the element edges facing each other, not their centers.
            Assert.AreEqual(40, line.SourceX);
            Assert.AreEqual(200, line.TargetX);
        }

        /// <summary>
        /// Verifies that resizing updates the geometry and clamps width and height to a minimum.
        /// </summary>
        [TestMethod]
        public void ResizeElementUpdatesGeometryWithMinimumClamp()
        {
            (ThreatModel model, Guid node, _, _) = Build();
            DiagramEditor editor = new DiagramEditor(model);
            DrawingSurfaceModel diagram = editor.Model.DrawingSurfaceList[0];

            editor.ResizeElement(diagram, node, 10, 12, 200, 150);
            Assert.AreEqual(10, Component(diagram, node).Left);
            Assert.AreEqual(12, Component(diagram, node).Top);
            Assert.AreEqual(200, Component(diagram, node).Width);
            Assert.AreEqual(150, Component(diagram, node).Height);

            editor.ResizeElement(diagram, node, 10, 12, 5, 3);
            Assert.AreEqual(20, Component(diagram, node).Width);
            Assert.AreEqual(20, Component(diagram, node).Height);
        }

        /// <summary>
        /// Verifies that setting a connector's handle bends it.
        /// </summary>
        [TestMethod]
        public void SetLineHandleBendsConnector()
        {
            (ThreatModel model, _, _, Guid connector) = Build();
            DiagramEditor editor = new DiagramEditor(model);
            DrawingSurfaceModel diagram = editor.Model.DrawingSurfaceList[0];

            editor.SetLineHandle(diagram, connector, 111, 222);

            Assert.AreEqual(111, Line(diagram, connector).HandleX);
            Assert.AreEqual(222, Line(diagram, connector).HandleY);
        }

        /// <summary>
        /// Verifies that setting an endpoint moves it and updates its attachment (or detaches it).
        /// </summary>
        [TestMethod]
        public void SetLineEndpointMovesAndAttaches()
        {
            (ThreatModel model, Guid node, _, Guid connector) = Build();
            DiagramEditor editor = new DiagramEditor(model);
            DrawingSurfaceModel diagram = editor.Model.DrawingSurfaceList[0];

            editor.SetLineEndpoint(diagram, connector, isSource: true, 7, 9, node);
            Assert.AreEqual(7, Line(diagram, connector).SourceX);
            Assert.AreEqual(9, Line(diagram, connector).SourceY);
            Assert.AreEqual(node, Line(diagram, connector).SourceGuid);

            editor.SetLineEndpoint(diagram, connector, isSource: true, 8, 10, Guid.Empty);
            Assert.AreEqual(Guid.Empty, Line(diagram, connector).SourceGuid);
        }

        /// <summary>
        /// Verifies that moving a node keeps a straight connector straight by re-centering its handle.
        /// </summary>
        [TestMethod]
        public void MovingNodeKeepsStraightConnectorStraight()
        {
            (ThreatModel model, Guid node, _, Guid connector) = Build();
            DiagramEditor editor = new DiagramEditor(model);
            DrawingSurfaceModel diagram = editor.Model.DrawingSurfaceList[0];

            editor.MoveElementBy(diagram, node, 10, 20);

            LineElement line = Line(diagram, connector);
            Assert.AreEqual((line.SourceX + line.TargetX) / 2, line.HandleX);
            Assert.AreEqual((line.SourceY + line.TargetY) / 2, line.HandleY);
        }

        /// <summary>
        /// Verifies that undo restores the pre-edit state and enables redo.
        /// </summary>
        [TestMethod]
        public void UndoRestoresPreviousStateAndEnablesRedo()
        {
            (ThreatModel model, Guid node, _, _) = Build();
            DiagramEditor editor = new DiagramEditor(model);
            DrawingSurfaceModel diagram = editor.Model.DrawingSurfaceList[0];

            editor.BeginChange();
            editor.MoveElementBy(diagram, node, 15, 25);
            Assert.AreEqual(15, Component(diagram, node).Left);

            editor.Undo();

            Assert.AreEqual(0, Component(editor.Model.DrawingSurfaceList[0], node).Left);
            Assert.IsTrue(editor.CanRedo);
            Assert.IsFalse(editor.CanUndo);
        }

        /// <summary>
        /// Verifies that redo re-applies an undone change.
        /// </summary>
        [TestMethod]
        public void RedoReappliesChange()
        {
            (ThreatModel model, Guid node, _, _) = Build();
            DiagramEditor editor = new DiagramEditor(model);

            editor.BeginChange();
            editor.MoveElementBy(editor.Model.DrawingSurfaceList[0], node, 15, 25);
            editor.Undo();
            editor.Redo();

            Assert.AreEqual(15, Component(editor.Model.DrawingSurfaceList[0], node).Left);
            Assert.IsTrue(editor.CanUndo);
            Assert.IsFalse(editor.CanRedo);
        }

        /// <summary>
        /// Verifies that starting a new change clears the redo history.
        /// </summary>
        [TestMethod]
        public void BeginChangeClearsRedoHistory()
        {
            (ThreatModel model, Guid node, _, _) = Build();
            DiagramEditor editor = new DiagramEditor(model);

            editor.BeginChange();
            editor.MoveElementBy(editor.Model.DrawingSurfaceList[0], node, 15, 25);
            editor.Undo();
            Assert.IsTrue(editor.CanRedo);

            editor.BeginChange();

            Assert.IsFalse(editor.CanRedo);
        }

        /// <summary>
        /// Verifies that the serialized bytes round-trip back to an equivalent model.
        /// </summary>
        [TestMethod]
        public void ToBytesRoundTrips()
        {
            (ThreatModel model, Guid node, _, _) = Build();
            DiagramEditor editor = new DiagramEditor(model);

            byte[] bytes = editor.ToBytes();
            using System.IO.MemoryStream stream = new System.IO.MemoryStream(bytes);
            ThreatModel reloaded = ThreatModel.Load(stream);

            Assert.AreEqual(1, reloaded.DrawingSurfaceList.Count);
            Assert.IsTrue(reloaded.DrawingSurfaceList[0].Borders.ContainsKey(node));
        }

        /// <summary>
        /// Verifies that finding an unknown element returns <see langword="null"/>.
        /// </summary>
        [TestMethod]
        public void FindElementReturnsNullForUnknownGuid()
        {
            (ThreatModel model, _, _, _) = Build();
            DiagramEditor editor = new DiagramEditor(model);

            Assert.IsNull(DiagramEditor.FindElement(editor.Model.DrawingSurfaceList[0], Guid.NewGuid()));
        }

        private static DrawingElement Component(DrawingSurfaceModel diagram, Guid guid)
        {
            return (DrawingElement)diagram.Borders[guid];
        }

        private static LineElement Line(DrawingSurfaceModel diagram, Guid guid)
        {
            return (LineElement)diagram.Lines[guid];
        }

        private static (ThreatModel Model, Guid Node, Guid Other, Guid Connector) Build()
        {
            Guid node = Guid.NewGuid();
            Guid other = Guid.NewGuid();
            Guid connector = Guid.NewGuid();

            StencilRectangle rectangle = new StencilRectangle { Guid = node, TypeId = "GE.P", Left = 0, Top = 0, Width = 40, Height = 40 };
            rectangle.Properties.Add(new StringDisplayAttribute { Name = "Name", Value = NodeName });

            StencilEllipse ellipse = new StencilEllipse { Guid = other, TypeId = "GE.PR", Left = 200, Top = 0, Width = 60, Height = 60 };
            Connector flow = new Connector { Guid = connector, TypeId = "GE.DF", SourceGuid = node, TargetGuid = other, SourceX = 40, SourceY = 20, TargetX = 200, TargetY = 30 };

            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "Main" };
            diagram.Borders[node] = rectangle;
            diagram.Borders[other] = ellipse;
            diagram.Lines[connector] = flow;

            ThreatModel model = new ThreatModel();
            model.DrawingSurfaceList.Add(diagram);
            return (model, node, other, connector);
        }
    }
}
