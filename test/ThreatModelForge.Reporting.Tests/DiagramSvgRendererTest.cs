namespace ThreatModelForge.Reporting.Tests
{
    using System;
    using System.Linq;
    using System.Xml.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Reporting;

    /// <summary>
    /// Unit tests for <see cref="DiagramSvgRenderer"/>.
    /// </summary>
    [TestClass]
    public class DiagramSvgRendererTest
    {
        private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

        /// <summary>
        /// Verifies that components, boundaries, connectors, and labels are rendered as SVG.
        /// </summary>
        [TestMethod]
        public void RendersElementsConnectorsAndLabels()
        {
            XElement svg = new DiagramSvgRenderer().Render(BuildDiagram());

            Assert.AreEqual(Svg + "svg", svg.Name);
            Assert.IsNotNull(svg.Attribute("viewBox"));
            Assert.IsTrue(svg.Descendants(Svg + "rect").Any(), "expected a rectangle");
            Assert.IsTrue(svg.Descendants(Svg + "ellipse").Any(), "expected an ellipse");
            Assert.IsTrue(svg.Descendants(Svg + "path").Any(p => string.Equals((string?)p.Attribute("class"), "tmf-connector", StringComparison.Ordinal)), "expected a connector path");
            Assert.IsTrue(svg.Descendants(Svg + "text").Any(t => string.Equals((string)t, "Web App", StringComparison.Ordinal)), "expected the element label");
        }

        /// <summary>
        /// Verifies that connector strokes and their labels use theme-aware classes, with a halo that
        /// keeps line text readable over either a light or dark diagram surface.
        /// </summary>
        [TestMethod]
        public void RendersThemeAwareConnectorAndLabel()
        {
            DrawingSurfaceModel diagram = BuildDiagram();
            Connector connector = diagram.Lines.Values.OfType<Connector>().Single();
            connector.Properties.Add(new StringDisplayAttribute { Name = "Name", Value = "HTTPS request" });

            XElement svg = new DiagramSvgRenderer().Render(diagram);

            XElement path = svg.Descendants(Svg + "path").Single(element => (string?)element.Attribute("class") == "tmf-connector");
            XElement label = svg.Descendants(Svg + "text").Single(element => (string?)element.Attribute("class") == "tmf-flow-label");
            string styles = svg.Descendants(Svg + "style").Single().Value;
            Assert.AreEqual("non-scaling-stroke", (string?)path.Attribute("vector-effect"));
            Assert.AreEqual("stroke", (string?)label.Attribute("paint-order"));
            StringAssert.Contains(styles, ".tmf-connector");
            StringAssert.Contains(styles, "prefers-color-scheme: dark");
        }

        /// <summary>
        /// Verifies that a long component name is wrapped and clipped to the shape while its complete
        /// value remains available as an SVG title.
        /// </summary>
        [TestMethod]
        public void WrapsAndClipsLongComponentLabel()
        {
            const string name = "A very long component name that cannot fit on one line";
            StencilRectangle component = new StencilRectangle
            {
                Guid = Guid.NewGuid(),
                TypeId = "GE.P",
                Left = 0,
                Top = 0,
                Width = 90,
                Height = 48,
            };
            component.Properties.Add(new StringDisplayAttribute { Name = "Name", Value = name });
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = Guid.NewGuid() };
            diagram.Borders[component.Guid] = component;

            XElement svg = new DiagramSvgRenderer().Render(diagram);

            XElement label = svg.Descendants(Svg + "text").Single(element => (string?)element.Attribute("class") == "tmf-node-label");
            Assert.IsTrue(label.Elements(Svg + "tspan").Count() > 1, "expected a wrapped label");
            StringAssert.StartsWith((string?)label.Attribute("clip-path"), "url(#tmf-label-");
            Assert.IsTrue(svg.Descendants(Svg + "clipPath").Any(), "expected the label to be clipped to its shape");
            Assert.IsTrue(svg.Descendants(Svg + "title").Any(element => element.Value == name), "expected the full label in a title");
        }

        /// <summary>
        /// Verifies that wrapping moves a word intact to the next row rather than splitting it merely
        /// to consume the unused space at the end of the current row.
        /// </summary>
        [TestMethod]
        public void WrapsComponentLabelAtWordBoundaries()
        {
            StencilRectangle component = new StencilRectangle
            {
                Guid = Guid.NewGuid(),
                TypeId = "GE.P",
                Left = 0,
                Top = 0,
                Width = 140,
                Height = 60,
            };
            component.Properties.Add(new StringDisplayAttribute { Name = "Name", Value = "Azure Front Door data-plane edge" });
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = Guid.NewGuid() };
            diagram.Borders[component.Guid] = component;

            XElement svg = new DiagramSvgRenderer().Render(diagram);

            string[] lines = svg.Descendants(Svg + "text")
                .Single(element => (string?)element.Attribute("class") == "tmf-node-label")
                .Elements(Svg + "tspan")
                .Select(element => element.Value)
                .ToArray();
            CollectionAssert.AreEqual(new[] { "Azure Front Door", "data-plane edge" }, lines);
        }

        /// <summary>
        /// Verifies that a null diagram is rejected.
        /// </summary>
        [TestMethod]
        public void RejectsNullDiagram()
        {
            Assert.Throws<ArgumentNullException>(() => new DiagramSvgRenderer().Render(null!));
        }

        /// <summary>
        /// Verifies that a trust-boundary line with an offset handle renders as a dashed quadratic
        /// curve (an SVG path with a <c>Q</c> command), not a straight segment.
        /// </summary>
        [TestMethod]
        public void RendersLineBoundaryAsDashedCurve()
        {
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = Guid.NewGuid() };
            LineBoundary boundary = new LineBoundary
            {
                Guid = Guid.NewGuid(),
                SourceX = 0,
                SourceY = 0,
                TargetX = 200,
                TargetY = 0,
                HandleX = 100,
                HandleY = 80,
            };
            diagram.Lines[boundary.Guid] = boundary;

            XElement svg = new DiagramSvgRenderer().Render(diagram);

            XElement? path = svg.Descendants(Svg + "path")
                .FirstOrDefault(p => string.Equals((string?)p.Attribute("stroke"), "#dc2626", StringComparison.Ordinal));
            Assert.IsNotNull(path, "expected a dashed boundary path");
            StringAssert.Contains((string?)path!.Attribute("d"), "Q");
            Assert.AreEqual("8 4", (string?)path.Attribute("stroke-dasharray"));
        }

        /// <summary>
        /// Verifies that a multi-page model renders one nested SVG (and title) per page, sharing a
        /// single hoisted defs so marker ids are not duplicated.
        /// </summary>
        [TestMethod]
        public void RenderModelStacksEveryPage()
        {
            ThreatModel model = new ThreatModel();
            DrawingSurfaceModel first = BuildDiagram();
            first.Header = "Context";
            DrawingSurfaceModel second = BuildDiagram();
            second.Header = "Payments";
            model.DrawingSurfaceList.Add(first);
            model.DrawingSurfaceList.Add(second);

            XElement svg = new DiagramSvgRenderer().RenderModel(model);

            Assert.AreEqual(Svg + "svg", svg.Name);
            Assert.AreEqual(2, svg.Elements(Svg + "svg").Count(), "expected one nested svg per page");
            Assert.AreEqual(1, svg.Descendants(Svg + "defs").Count(), "expected a single shared defs");
            Assert.IsTrue(svg.Elements(Svg + "text").Any(t => string.Equals((string)t, "Context", StringComparison.Ordinal)));
            Assert.IsTrue(svg.Elements(Svg + "text").Any(t => string.Equals((string)t, "Payments", StringComparison.Ordinal)));
        }

        /// <summary>
        /// Verifies that a single-page model renders as one diagram SVG, not a stacked wrapper.
        /// </summary>
        [TestMethod]
        public void RenderModelSinglePageIsNotWrapped()
        {
            ThreatModel model = new ThreatModel();
            model.DrawingSurfaceList.Add(BuildDiagram());

            XElement svg = new DiagramSvgRenderer().RenderModel(model);

            Assert.AreEqual(Svg + "svg", svg.Name);
            Assert.IsFalse(svg.Elements(Svg + "svg").Any(), "a single page should not be wrapped in nested svgs");
        }

        private static DrawingSurfaceModel BuildDiagram()
        {
            StencilRectangle rectangle = new StencilRectangle { Guid = Guid.NewGuid(), TypeId = "GE.P", Left = 0, Top = 0, Width = 80, Height = 40 };
            rectangle.Properties.Add(new StringDisplayAttribute { Name = "Name", Value = "Web App" });

            StencilEllipse ellipse = new StencilEllipse { Guid = Guid.NewGuid(), TypeId = "GE.PR", Left = 200, Top = 0, Width = 60, Height = 60 };
            BorderBoundary boundary = new BorderBoundary { Guid = Guid.NewGuid(), TypeId = "GE.TB", Left = -10, Top = -10, Width = 320, Height = 100 };
            Connector flow = new Connector { Guid = Guid.NewGuid(), TypeId = "GE.DF", SourceX = 80, SourceY = 20, TargetX = 200, TargetY = 30 };

            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "Main DFD" };
            diagram.Borders[rectangle.Guid] = rectangle;
            diagram.Borders[ellipse.Guid] = ellipse;
            diagram.Borders[boundary.Guid] = boundary;
            diagram.Lines[flow.Guid] = flow;
            return diagram;
        }
    }
}
