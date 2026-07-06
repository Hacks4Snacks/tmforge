namespace ThreatModelForge.Editing.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for <see cref="DiagramGeometry"/>.
    /// </summary>
    [TestClass]
    public class DiagramGeometryTest
    {
        /// <summary>
        /// Verifies that the bounds enclose every component and line in the diagram.
        /// </summary>
        [TestMethod]
        public void GetBoundsEnclosesAllElements()
        {
            StencilRectangle rectangle = new StencilRectangle { Guid = Guid.NewGuid(), Left = 0, Top = 0, Width = 40, Height = 40 };
            StencilEllipse ellipse = new StencilEllipse { Guid = Guid.NewGuid(), Left = 200, Top = 0, Width = 60, Height = 60 };
            Connector flow = new Connector { Guid = Guid.NewGuid(), SourceX = 20, SourceY = 20, TargetX = 230, TargetY = 30 };

            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = Guid.NewGuid() };
            diagram.Borders[rectangle.Guid] = rectangle;
            diagram.Borders[ellipse.Guid] = ellipse;
            diagram.Lines[flow.Guid] = flow;

            (int minX, int minY, int maxX, int maxY) = DiagramGeometry.GetBounds(diagram);

            Assert.AreEqual(0, minX);
            Assert.AreEqual(0, minY);
            Assert.AreEqual(260, maxX);
            Assert.AreEqual(60, maxY);
        }

        /// <summary>
        /// Verifies that an empty diagram returns the default bounds.
        /// </summary>
        [TestMethod]
        public void GetBoundsEmptyReturnsDefault()
        {
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = Guid.NewGuid() };

            (int minX, int minY, int maxX, int maxY) = DiagramGeometry.GetBounds(diagram);

            Assert.AreEqual(0, minX);
            Assert.AreEqual(0, minY);
            Assert.AreEqual(100, maxX);
            Assert.AreEqual(100, maxY);
        }

        /// <summary>
        /// Verifies that ElementAt finds the component containing a point and ignores boundaries.
        /// </summary>
        [TestMethod]
        public void ElementAtFindsContainingComponent()
        {
            Guid component = Guid.NewGuid();
            StencilRectangle rectangle = new StencilRectangle { Guid = component, Left = 10, Top = 10, Width = 40, Height = 30 };
            BorderBoundary boundary = new BorderBoundary { Guid = Guid.NewGuid(), Left = 0, Top = 0, Width = 200, Height = 200 };
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = Guid.NewGuid() };
            diagram.Borders[rectangle.Guid] = rectangle;
            diagram.Borders[boundary.Guid] = boundary;

            Assert.AreEqual(component, DiagramGeometry.ElementAt(diagram, 20, 20));
            Assert.AreEqual(Guid.Empty, DiagramGeometry.ElementAt(diagram, 500, 500));
        }

        /// <summary>
        /// Verifies that CenterOf returns an element's center.
        /// </summary>
        [TestMethod]
        public void CenterOfReturnsElementCenter()
        {
            Guid component = Guid.NewGuid();
            StencilEllipse ellipse = new StencilEllipse { Guid = component, Left = 100, Top = 50, Width = 40, Height = 20 };
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = Guid.NewGuid() };
            diagram.Borders[ellipse.Guid] = ellipse;

            (int x, int y) = DiagramGeometry.CenterOf(diagram, component);

            Assert.AreEqual(120, x);
            Assert.AreEqual(60, y);
        }

        /// <summary>
        /// Verifies that EdgePoint lands on the element's border facing the external point.
        /// </summary>
        [TestMethod]
        public void EdgePointLandsOnBorderFacingTarget()
        {
            Guid component = Guid.NewGuid();
            StencilRectangle rectangle = new StencilRectangle { Guid = component, Left = 0, Top = 0, Width = 40, Height = 40 };
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = Guid.NewGuid() };
            diagram.Borders[rectangle.Guid] = rectangle;

            (int rightX, int rightY) = DiagramGeometry.EdgePoint(diagram, component, 500, 20);
            Assert.AreEqual(40, rightX);
            Assert.AreEqual(20, rightY);

            (int downX, int downY) = DiagramGeometry.EdgePoint(diagram, component, 20, 500);
            Assert.AreEqual(20, downX);
            Assert.AreEqual(40, downY);
        }
    }
}
