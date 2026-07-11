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
        private const int ComponentFontSize = 12;
        private const int ComponentLineHeight = 14;
        private const int MinimumComponentFontSize = 9;
        private const int Padding = 24;
        private const string SvgStyles =
            ".tmf-diagram { background: var(--tmf-diagram-surface, #ffffff); }"
            + " .tmf-connector { stroke: var(--tmf-connector, #475569); }"
            + " .tmf-arrow { fill: var(--tmf-connector, #475569); }"
            + " .tmf-flow-label { fill: var(--tmf-flow-label, #242424); stroke: var(--tmf-flow-halo, #ffffff);"
            + " stroke-width: 4px; stroke-linejoin: round; paint-order: stroke; font-weight: 600; }"
            + " .tmf-page-title { fill: var(--tmf-page-title, #242424); }"
            + " .tmf-boundary { stroke: var(--tmf-boundary, #dc2626); }"
            + " @media (prefers-color-scheme: dark) { .tmf-diagram { --tmf-diagram-surface: #202020;"
            + " --tmf-connector: #f3f4f6; --tmf-flow-label: #f9fafb; --tmf-flow-halo: #202020;"
            + " --tmf-page-title: #f9fafb; --tmf-boundary: #f87171; } }";

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
                new XAttribute("class", "tmf-diagram"),
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

        /// <summary>
        /// Renders every diagram in the model to a single SVG, stacking the pages vertically with a
        /// title above each. A single-page model renders as just that diagram; an empty model
        /// renders as an empty SVG. The pages share one hoisted <c>&lt;defs&gt;</c> so marker ids
        /// are not duplicated.
        /// </summary>
        /// <param name="model">The threat model whose diagrams to render.</param>
        /// <returns>The composed <c>&lt;svg&gt;</c> element.</returns>
        public XElement RenderModel(ThreatModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            List<DrawingSurfaceModel> surfaces = model.DrawingSurfaceList.ToList();
            if (surfaces.Count == 0)
            {
                return new XElement(Svg + "svg", new XAttribute("role", "img"));
            }

            if (surfaces.Count == 1)
            {
                return this.Render(surfaces[0]);
            }

            const int gap = 32;
            const int titleHeight = 28;
            int offsetY = 0;
            int maxWidth = 1;
            XElement? sharedDefs = null;
            List<XElement> body = new List<XElement>();

            foreach (DrawingSurfaceModel surface in surfaces)
            {
                XElement page = this.Render(surface);
                int width = ParseDimension(page, "width");
                int height = ParseDimension(page, "height");
                maxWidth = Math.Max(maxWidth, width);

                XElement? defs = page.Element(Svg + "defs");
                if (defs != null)
                {
                    defs.Remove();
                    sharedDefs ??= defs;
                }

                body.Add(new XElement(
                    Svg + "text",
                    new XAttribute("class", "tmf-page-title"),
                    new XAttribute("x", 4),
                    new XAttribute("y", offsetY + 18),
                    new XAttribute("font-family", "'Segoe UI', Helvetica, sans-serif"),
                    new XAttribute("font-size", 16),
                    new XAttribute("font-weight", "bold"),
                    new XAttribute("fill", "#242424"),
                    string.IsNullOrEmpty(surface.Header) ? "Diagram" : surface.Header!));

                page.SetAttributeValue("x", 0);
                page.SetAttributeValue("y", offsetY + titleHeight);
                body.Add(page);

                offsetY += titleHeight + height + gap;
            }

            int totalHeight = Math.Max(1, offsetY - gap);
            XElement root = new XElement(
                Svg + "svg",
                new XAttribute("class", "tmf-diagram tmf-diagram-stack"),
                new XAttribute("viewBox", string.Format(CultureInfo.InvariantCulture, "0 0 {0} {1}", maxWidth, totalHeight)),
                new XAttribute("width", maxWidth),
                new XAttribute("height", totalHeight),
                new XAttribute("role", "img"));
            if (sharedDefs != null)
            {
                root.Add(sharedDefs);
            }

            foreach (XElement element in body)
            {
                root.Add(element);
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

        private static int ParseDimension(XElement svg, string name)
        {
            return int.TryParse(svg.Attribute(name)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 1;
        }

        private static XElement BuildDefs()
        {
            return new XElement(
                Svg + "defs",
                new XElement(Svg + "style", new XAttribute("type", "text/css"), SvgStyles),
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
                        new XAttribute("class", "tmf-arrow"),
                        new XAttribute("d", "M0,0 L0,6 L9,3 z"),
                        new XAttribute("fill", "#475569"))));
        }

        private static XElement RenderBorderBoundary(BorderBoundary boundary)
        {
            return new XElement(
                Svg + "rect",
                new XAttribute("class", "tmf-boundary"),
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
                new XAttribute("class", "tmf-boundary"),
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
                new XAttribute("class", "tmf-connector"),
                new XAttribute("d", string.Format(CultureInfo.InvariantCulture, "M {0},{1} Q {2},{3} {4},{5}", connector.SourceX, connector.SourceY, handleX, handleY, connector.TargetX, connector.TargetY)),
                new XAttribute("fill", "none"),
                new XAttribute("stroke", "#475569"),
                new XAttribute("stroke-width", 2.25),
                new XAttribute("vector-effect", "non-scaling-stroke"),
                new XAttribute("marker-end", "url(#arrow)")));

            string name = GetElementName(connector);
            if (!string.IsNullOrEmpty(name))
            {
                int labelX = (connector.SourceX + (2 * handleX) + connector.TargetX) / 4;
                int labelY = ((connector.SourceY + (2 * handleY) + connector.TargetY) / 4) - 4;
                group.Add(CreateFlowLabel(labelX, labelY, name));
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
            string name = GetElementName(element);
            if (!string.IsNullOrEmpty(name))
            {
                group.Add(new XElement(Svg + "title", name));
            }

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

            if (!string.IsNullOrEmpty(name))
            {
                string clipId = "tmf-label-" + element.Guid.ToString("N");
                group.Add(CreateLabelClip(element, clipId));
                group.Add(CreateComponentLabel(element, centerX, centerY, name, clipId));
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

        private static XElement CreateFlowLabel(int x, int y, string text)
        {
            return new XElement(
                Svg + "text",
                new XAttribute("class", "tmf-flow-label"),
                new XAttribute("x", x),
                new XAttribute("y", y),
                new XAttribute("text-anchor", "middle"),
                new XAttribute("dominant-baseline", "middle"),
                new XAttribute("font-family", "Segoe UI, Helvetica, sans-serif"),
                new XAttribute("font-size", 13),
                new XAttribute("fill", "#242424"),
                new XAttribute("stroke", "#ffffff"),
                new XAttribute("stroke-width", 4),
                new XAttribute("paint-order", "stroke"),
                text);
        }

        private static XElement CreateLabelClip(DrawingElement element, string clipId)
        {
            XElement shape;
            if (element is StencilEllipse)
            {
                shape = new XElement(
                    Svg + "ellipse",
                    new XAttribute("cx", element.Left + (element.Width / 2)),
                    new XAttribute("cy", element.Top + (element.Height / 2)),
                    new XAttribute("rx", Math.Max(1, element.Width / 2)),
                    new XAttribute("ry", Math.Max(1, element.Height / 2)));
            }
            else
            {
                shape = new XElement(
                    Svg + "rect",
                    new XAttribute("x", element.Left),
                    new XAttribute("y", element.Top),
                    new XAttribute("width", Math.Max(0, element.Width)),
                    new XAttribute("height", Math.Max(0, element.Height)));
            }

            return new XElement(Svg + "clipPath", new XAttribute("id", clipId), shape);
        }

        private static XElement CreateComponentLabel(
            DrawingElement element,
            int centerX,
            int centerY,
            string text,
            string clipId)
        {
            int horizontalPadding = element is StencilEllipse ? Math.Max(8, element.Width / 5) : 10;
            int verticalPadding = element is StencilEllipse ? Math.Max(6, element.Height / 5) : 6;
            int availableWidth = Math.Max(1, element.Width - (2 * horizontalPadding));
            int availableHeight = Math.Max(1, element.Height - (2 * verticalPadding));
            int fontSize = ComponentFontSize;
            int lineHeight = ComponentLineHeight;
            int maxCharacters;
            int maxLines;
            List<string> lines;

            do
            {
                lineHeight = fontSize + 2;
                maxCharacters = Math.Max(1, (int)Math.Floor(availableWidth / (fontSize * 0.56)));
                maxLines = Math.Max(1, availableHeight / lineHeight);
                lines = WrapText(text, maxCharacters);
                if (lines.Count <= maxLines || fontSize == MinimumComponentFontSize)
                {
                    break;
                }

                fontSize--;
            }
            while (fontSize >= MinimumComponentFontSize);

            lines = TruncateLines(lines, maxLines, maxCharacters);
            int firstY = centerY - (((lines.Count - 1) * lineHeight) / 2);
            XElement label = new XElement(
                Svg + "text",
                new XAttribute("class", "tmf-node-label"),
                new XAttribute("clip-path", "url(#" + clipId + ")"),
                new XAttribute("text-anchor", "middle"),
                new XAttribute("font-family", "Segoe UI, Helvetica, sans-serif"),
                new XAttribute("font-size", fontSize),
                new XAttribute("fill", "#111111"));

            for (int i = 0; i < lines.Count; i++)
            {
                label.Add(new XElement(
                    Svg + "tspan",
                    new XAttribute("x", centerX),
                    new XAttribute("y", firstY + (i * lineHeight)),
                    new XAttribute("dominant-baseline", "middle"),
                    lines[i]));
            }

            return label;
        }

        private static List<string> WrapText(string text, int maxCharacters)
        {
            List<string> lines = new List<string>();
            string current = string.Empty;
            foreach (string word in text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (word.Length <= maxCharacters)
                {
                    if (current.Length == 0)
                    {
                        current = word;
                        continue;
                    }

                    if (current.Length + 1 + word.Length <= maxCharacters)
                    {
                        current += " " + word;
                        continue;
                    }

                    lines.Add(current);
                    current = word;
                    continue;
                }

                if (current.Length > 0)
                {
                    lines.Add(current);
                    current = string.Empty;
                }

                int offset = 0;
                while (word.Length - offset > maxCharacters)
                {
                    lines.Add(word.Substring(offset, maxCharacters));
                    offset += maxCharacters;
                }

                if (offset < word.Length)
                {
                    current = word.Substring(offset);
                }
            }

            if (current.Length > 0)
            {
                lines.Add(current);
            }

            return lines.Count == 0 ? new List<string> { string.Empty } : lines;
        }

        private static List<string> TruncateLines(List<string> lines, int maxLines, int maxCharacters)
        {
            if (lines.Count <= maxLines)
            {
                return lines;
            }

            List<string> visible = lines.Take(maxLines).ToList();
            int last = visible.Count - 1;
            int keep = Math.Max(1, maxCharacters - 3);
            string finalLine = visible[last].Length > keep ? visible[last].Substring(0, keep) : visible[last];
            visible[last] = finalLine.TrimEnd() + "...";
            return visible;
        }

        private static string GetElementName(Entity element)
        {
            foreach (StringDisplayAttribute property in element.Properties.OfType<StringDisplayAttribute>()
                .Where(property => string.Equals(property.Name, "Name", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(property.DisplayName, "Name", StringComparison.OrdinalIgnoreCase)))
            {
                return property.Value as string ?? string.Empty;
            }

            return string.Empty;
        }
    }
}
