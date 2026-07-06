namespace ThreatModelForge.Editing.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for <see cref="DiagramLayout"/>.
    /// </summary>
    [TestClass]
    public class DiagramLayoutTest
    {
        private static readonly Guid NodeA = new Guid("00000000-0000-0000-0000-0000000000a1");
        private static readonly Guid NodeB = new Guid("00000000-0000-0000-0000-0000000000b2");
        private static readonly Guid NodeC = new Guid("00000000-0000-0000-0000-0000000000c3");

        /// <summary>
        /// Verifies that a connected source is placed in an earlier (left) layer than its target.
        /// </summary>
        [TestMethod]
        public void ApplyPlacesConnectedSourceLeftOfTarget()
        {
            DrawingSurfaceModel diagram = BuildChain(NodeA, NodeB);

            DiagramLayout.Apply(diagram);

            Assert.IsTrue(Component(diagram, NodeA).Left < Component(diagram, NodeB).Left);
        }

        /// <summary>
        /// Verifies that layout is a pure function of the input: two identical diagrams lay out identically.
        /// </summary>
        [TestMethod]
        public void ApplyIsDeterministic()
        {
            DrawingSurfaceModel first = BuildChain(NodeA, NodeB, NodeC);
            DrawingSurfaceModel second = BuildChain(NodeA, NodeB, NodeC);

            DiagramLayout.Apply(first);
            DiagramLayout.Apply(second);

            foreach (Guid guid in new[] { NodeA, NodeB, NodeC })
            {
                Assert.AreEqual(Component(first, guid).Left, Component(second, guid).Left);
                Assert.AreEqual(Component(first, guid).Top, Component(second, guid).Top);
            }
        }

        /// <summary>
        /// Verifies that no two components overlap after layout.
        /// </summary>
        [TestMethod]
        public void ApplyProducesNoOverlappingComponents()
        {
            DrawingSurfaceModel diagram = BuildChain(NodeA, NodeB, NodeC);

            // Add a second, disconnected node in the same starting layer as A to exercise stacking.
            Guid extra = new Guid("00000000-0000-0000-0000-0000000000d4");
            AddComponent(diagram, extra);

            DiagramLayout.Apply(diagram);

            List<DrawingElement> components = diagram.Borders.Values.OfType<DrawingElement>().ToList();
            for (int i = 0; i < components.Count; i++)
            {
                for (int j = i + 1; j < components.Count; j++)
                {
                    Assert.IsFalse(Overlaps(components[i], components[j]), "components must not overlap");
                }
            }
        }

        /// <summary>
        /// Verifies that connector endpoints are re-routed to the element edges facing each other.
        /// </summary>
        [TestMethod]
        public void ApplyReroutesConnectorEndpointsToElementEdges()
        {
            DrawingSurfaceModel diagram = BuildChain(NodeA, NodeB);
            Connector connector = diagram.Lines.Values.OfType<Connector>().Single();

            DiagramLayout.Apply(diagram);

            DrawingElement source = Component(diagram, NodeA);
            DrawingElement target = Component(diagram, NodeB);

            // A is left of B on the same row, so the flow leaves A's right edge and enters B's left edge.
            Assert.AreEqual(source.Left + source.Width, connector.SourceX);
            Assert.AreEqual(target.Left, connector.TargetX);
            Assert.AreEqual((connector.SourceX + connector.TargetX) / 2, connector.HandleX);
        }

        /// <summary>
        /// Verifies that trust boundaries are not moved by the component layout.
        /// </summary>
        [TestMethod]
        public void ApplyLeavesTrustBoundariesUntouched()
        {
            DrawingSurfaceModel diagram = BuildChain(NodeA, NodeB);
            Guid boundaryGuid = new Guid("00000000-0000-0000-0000-0000000000e5");
            BorderBoundary boundary = new BorderBoundary { Guid = boundaryGuid, Left = 500, Top = 600, Width = 300, Height = 200 };
            diagram.Borders[boundaryGuid] = boundary;

            DiagramLayout.Apply(diagram);

            Assert.AreEqual(500, boundary.Left);
            Assert.AreEqual(600, boundary.Top);
        }

        /// <summary>
        /// Verifies that a cyclic graph lays out without hanging and places each node distinctly.
        /// </summary>
        [TestMethod]
        public void ApplyTerminatesOnCycle()
        {
            DrawingSurfaceModel diagram = BuildChain(NodeA, NodeB);
            AddConnector(diagram, NodeB, NodeA);

            DiagramLayout.Apply(diagram);

            Assert.AreNotEqual(Component(diagram, NodeA).Left, Component(diagram, NodeB).Left);
        }

        /// <summary>
        /// Verifies that an empty diagram is a no-op rather than an error.
        /// </summary>
        [TestMethod]
        public void ApplyIgnoresEmptyDiagram()
        {
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "Empty" };

            DiagramLayout.Apply(diagram);

            Assert.AreEqual(0, diagram.Borders.Count);
        }

        /// <summary>
        /// Verifies that a null diagram throws.
        /// </summary>
        [TestMethod]
        public void ApplyThrowsOnNullDiagram()
        {
            Assert.Throws<ArgumentNullException>(() => DiagramLayout.Apply(null!));
        }

        private static bool Overlaps(DrawingElement first, DrawingElement second)
        {
            return !(first.Left + first.Width <= second.Left
                || second.Left + second.Width <= first.Left
                || first.Top + first.Height <= second.Top
                || second.Top + second.Height <= first.Top);
        }

        private static DrawingElement Component(DrawingSurfaceModel diagram, Guid guid)
        {
            return (DrawingElement)diagram.Borders[guid];
        }

        private static void AddComponent(DrawingSurfaceModel diagram, Guid guid)
        {
            diagram.Borders[guid] = new StencilEllipse { Guid = guid, TypeId = "GE.P", GenericTypeId = "GE.P", Width = 100, Height = 60 };
        }

        private static void AddConnector(DrawingSurfaceModel diagram, Guid source, Guid target)
        {
            Guid guid = Guid.NewGuid();
            diagram.Lines[guid] = new Connector { Guid = guid, TypeId = "GE.DF", SourceGuid = source, TargetGuid = target };
        }

        private static DrawingSurfaceModel BuildChain(params Guid[] nodes)
        {
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "Main" };
            foreach (Guid node in nodes)
            {
                AddComponent(diagram, node);
            }

            for (int i = 0; i < nodes.Length - 1; i++)
            {
                AddConnector(diagram, nodes[i], nodes[i + 1]);
            }

            return diagram;
        }
    }
}
