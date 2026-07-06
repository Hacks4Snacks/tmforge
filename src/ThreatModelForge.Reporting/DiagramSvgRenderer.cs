namespace ThreatModelForge.Reporting
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Xml.Linq;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Renders a data-flow diagram (a <see cref="DrawingSurfaceModel"/>) to an inline SVG element
    /// using only the model geometry, so no native graphics libraries are required.
    /// </summary>
    public sealed class DiagramSvgRenderer
    {
        private const int Padding = 24;

        private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

        /// <summary>
        /// Renders the given diagram to an SVG element.
        /// </summary>
        /// <param name="diagram">The diagram to render.</param>
        /// <returns>The <c>&lt;svg&gt;</c> element.</returns>
        public XElement Render(DrawingSurfaceModel diagram)
        {
            if (diagram == null)
            {
                throw new ArgumentNullException(nameof(diagram));
            }

            (int minX, int minY, int maxX, int maxY) = ComputeBounds(diagram);
            int width = Math.Max(1, (maxX - minX) + (2 * Padding));
            int height = Math.Max(1, (maxY - minY) + (2 * Padding));

            XElement root = new XElement(
                Svg + "svg",
                new XAttribute("viewBox", string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3}", minX - Padding, minY - Padding, width, height)),
                new XAttribute("width", width),
                new XAttribute("height", height),
                new XAttribute("role", "img"),
                BuildDefs());

            foreach (BorderBoundary boundary in diagram.Borders.Values.OfType<BorderBoundary>())
            {
                root.Add(RenderBorderBoundary(boundary));
            }

            foreach (LineBoundary boundary in diagram.Lines.Values.OfType<LineBoundary>())
            {
                root.Add(RenderLineBoundary(boundary));
            }

            foreach (Connector connector in diagram.Lines.Values.OfType<Connector>())
            {
                root.Add(RenderConnector(connector));
            }

            foreach (DrawingElement element in diagram.Borders.Values.OfType<DrawingElement>().Where(e => !(e is BorderBoundary)))
            {
                root.Add(RenderComponent(element));
            }

            return root;
        }

        private static (int MinX, int MinY, int MaxX, int MaxY) ComputeBounds(DrawingSurfaceModel diagram)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            foreach (DrawingElement element in diagram.Borders.Values.OfType<DrawingElement>())
            {
                minX = Math.Min(minX, element.Left);
                minY = Math.Min(minY, element.Top);
                maxX = Math.Max(maxX, element.Left + element.Width);
                maxY = Math.Max(maxY, element.Top + element.Height);
            }

            foreach (LineElement line in diagram.Lines.Values.OfType<LineElement>())
            {
                minX = Math.Min(minX, Math.Min(line.SourceX, line.TargetX));
                minY = Math.Min(minY, Math.Min(line.SourceY, line.TargetY));
                maxX = Math.Max(maxX, Math.Max(line.SourceX, line.TargetX));
                maxY = Math.Max(maxY, Math.Max(line.SourceY, line.TargetY));
            }

            if (minX == int.MaxValue)
            {
                return (0, 0, 100, 100);
            }

            return (minX, minY, maxX, maxY);
        }

        private static XElement BuildDefs()
        {
            return new XElement(
                Svg + "defs",
                new XElement(
                    Svg + "marker",
                    new XAttribute("id", "arrow"),
                    new XAttribute("markerWidth", 10),
                    new XAttribute("markerHeight", 10),
                    new XAttribute("refX", 9),
                    new XAttribute("refY", 3),
                    new XAttribute("orient", "auto"),
                    new XAttribute("markerUnits", "strokeWidth"),
                    new XElement(
                        Svg + "path",
                        new XAttribute("d", "M0,0 L0,6 L9,3 z"),
                        new XAttribute("fill", "#333333"))));
        }

        private static XElement RenderBorderBoundary(BorderBoundary boundary)
        {
            return new XElement(
                Svg + "rect",
                new XAttribute("x", boundary.Left),
                new XAttribute("y", boundary.Top),
                new XAttribute("width", Math.Max(0, boundary.Width)),
                new XAttribute("height", Math.Max(0, boundary.Height)),
                new XAttribute("rx", 8),
                new XAttribute("fill", "none"),
                new XAttribute("stroke", "#dc2626"),
                new XAttribute("stroke-width", 2),
                new XAttribute("stroke-dasharray", "8 4"));
        }

        private static XElement RenderLineBoundary(LineBoundary boundary)
        {
            (int handleX, int handleY) = EffectiveHandle(boundary);
            return new XElement(
                Svg + "path",
                new XAttribute("d", string.Format(CultureInfo.InvariantCulture, "M {0},{1} Q {2},{3} {4},{5}", boundary.SourceX, boundary.SourceY, handleX, handleY, boundary.TargetX, boundary.TargetY)),
                new XAttribute("fill", "none"),
                new XAttribute("stroke", "#dc2626"),
                new XAttribute("stroke-width", 2),
                new XAttribute("stroke-dasharray", "8 4"));
        }

        private static XElement RenderConnector(Connector connector)
        {
            XElement group = new XElement(Svg + "g");
            (int handleX, int handleY) = EffectiveHandle(connector);
            group.Add(new XElement(
                Svg + "path",
                new XAttribute("d", string.Format(CultureInfo.InvariantCulture, "M {0},{1} Q {2},{3} {4},{5}", connector.SourceX, connector.SourceY, handleX, handleY, connector.TargetX, connector.TargetY)),
                new XAttribute("fill", "none"),
                new XAttribute("stroke", "#333333"),
                new XAttribute("stroke-width", 1.5),
                new XAttribute("marker-end", "url(#arrow)")));

            string name = GetElementName(connector);
            if (!string.IsNullOrEmpty(name))
            {
                int labelX = (connector.SourceX + (2 * handleX) + connector.TargetX) / 4;
                int labelY = ((connector.SourceY + (2 * handleY) + connector.TargetY) / 4) - 4;
                group.Add(CreateLabel(labelX, labelY, name, "#333333"));
            }

            return group;
        }

        private static (int X, int Y) EffectiveHandle(LineElement line)
        {
            if (line.HandleX == 0 && line.HandleY == 0)
            {
                return ((line.SourceX + line.TargetX) / 2, (line.SourceY + line.TargetY) / 2);
            }

            return (line.HandleX, line.HandleY);
        }

        private static XElement RenderComponent(DrawingElement element)
        {
            XElement group = new XElement(Svg + "g");
            int centerX = element.Left + (element.Width / 2);
            int centerY = element.Top + (element.Height / 2);

            if (element is StencilEllipse)
            {
                group.Add(new XElement(
                    Svg + "ellipse",
                    new XAttribute("cx", centerX),
                    new XAttribute("cy", centerY),
                    new XAttribute("rx", Math.Max(1, element.Width / 2)),
                    new XAttribute("ry", Math.Max(1, element.Height / 2)),
                    new XAttribute("fill", "#e0e7ff"),
                    new XAttribute("stroke", "#4f46e5"),
                    new XAttribute("stroke-width", 1.5)));
            }
            else if (element is StencilParallelLines)
            {
                group.Add(new XElement(
                    Svg + "rect",
                    new XAttribute("x", element.Left),
                    new XAttribute("y", element.Top),
                    new XAttribute("width", Math.Max(0, element.Width)),
                    new XAttribute("height", Math.Max(0, element.Height)),
                    new XAttribute("fill", "#ecfdf5"),
                    new XAttribute("stroke", "none")));
                group.Add(HorizontalRule(element.Left, element.Left + element.Width, element.Top));
                group.Add(HorizontalRule(element.Left, element.Left + element.Width, element.Top + element.Height));
            }
            else
            {
                group.Add(new XElement(
                    Svg + "rect",
                    new XAttribute("x", element.Left),
                    new XAttribute("y", element.Top),
                    new XAttribute("width", Math.Max(0, element.Width)),
                    new XAttribute("height", Math.Max(0, element.Height)),
                    new XAttribute("rx", 3),
                    new XAttribute("fill", "#fef3c7"),
                    new XAttribute("stroke", "#d97706"),
                    new XAttribute("stroke-width", 1.5)));
            }

            string name = GetElementName(element);
            if (!string.IsNullOrEmpty(name))
            {
                group.Add(CreateLabel(centerX, centerY, name, "#111111"));
            }

            return group;
        }

        private static XElement HorizontalRule(int x1, int x2, int y)
        {
            return new XElement(
                Svg + "line",
                new XAttribute("x1", x1),
                new XAttribute("y1", y),
                new XAttribute("x2", x2),
                new XAttribute("y2", y),
                new XAttribute("stroke", "#059669"),
                new XAttribute("stroke-width", 2));
        }

        private static XElement CreateLabel(int x, int y, string text, string color)
        {
            return new XElement(
                Svg + "text",
                new XAttribute("x", x),
                new XAttribute("y", y),
                new XAttribute("text-anchor", "middle"),
                new XAttribute("dominant-baseline", "middle"),
                new XAttribute("font-family", "Segoe UI, Helvetica, sans-serif"),
                new XAttribute("font-size", 12),
                new XAttribute("fill", color),
                text);
        }

        private static string GetElementName(Entity element)
        {
            foreach (StringDisplayAttribute property in element.Properties.OfType<StringDisplayAttribute>())
            {
                if (string.Equals(property.Name, "Name", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(property.DisplayName, "Name", StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value as string ?? string.Empty;
                }
            }

            return string.Empty;
        }
    }
}
