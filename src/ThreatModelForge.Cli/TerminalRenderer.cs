namespace ThreatModelForge.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Renders a threat model's diagrams to a character-cell canvas for terminal display: processes
    /// as rounded boxes, external interactors as sharp boxes, data stores as parallel lines, trust
    /// boundaries as bold boxes, and data flows as arrowed lines. Dependency-free and distinct from
    /// the SVG report renderer.
    /// </summary>
    internal static class TerminalRenderer
    {
        private const int Process = 0;
        private const int External = 1;
        private const int DataStore = 2;
        private const int Boundary = 3;

        private const int ColorProcess = 36;
        private const int ColorExternal = 33;
        private const int ColorStore = 32;
        private const int ColorBoundary = 31;
        private const int ColorFlow = 90;

        /// <summary>
        /// Renders every diagram in the model to a single string.
        /// </summary>
        /// <param name="model">The model to render.</param>
        /// <param name="width">The canvas width in characters.</param>
        /// <param name="height">The canvas height in characters.</param>
        /// <param name="unicode">Whether to use Unicode box-drawing (otherwise ASCII).</param>
        /// <param name="color">Whether to emit ANSI color escapes.</param>
        /// <returns>The rendered diagrams.</returns>
        public static string Render(ThreatModel model, int width, int height, bool unicode, bool color)
        {
            StringBuilder output = new StringBuilder();
            if (model.DrawingSurfaceList.Count == 0)
            {
                output.Append("(no diagrams)\n");
                return output.ToString();
            }

            bool first = true;
            foreach (DrawingSurfaceModel diagram in model.DrawingSurfaceList)
            {
                if (!first)
                {
                    output.Append('\n');
                }

                first = false;
                output.Append("Diagram: " + (string.IsNullOrEmpty(diagram.Header) ? "(untitled)" : diagram.Header) + "\n");
                output.Append(RenderDiagram(diagram, width, height, unicode, color));
            }

            return output.ToString();
        }

        private static string RenderDiagram(DrawingSurfaceModel diagram, int width, int height, bool unicode, bool color)
        {
            List<DrawingElement> elements = diagram.Borders.Values.OfType<DrawingElement>().ToList();
            List<Connector> connectors = diagram.Lines.Values.OfType<Connector>().ToList();
            List<LineBoundary> lineBoundaries = diagram.Lines.Values.OfType<LineBoundary>().ToList();

            List<(int X, int Y)> points = new List<(int X, int Y)>();
            foreach (DrawingElement element in elements)
            {
                points.Add((element.Left, element.Top));
                points.Add((element.Left + element.Width, element.Top + element.Height));
            }

            foreach (Connector connector in connectors)
            {
                points.Add((connector.SourceX, connector.SourceY));
                points.Add((connector.TargetX, connector.TargetY));
            }

            foreach (LineBoundary line in lineBoundaries)
            {
                points.Add((line.SourceX, line.SourceY));
                points.Add((line.TargetX, line.TargetY));
            }

            if (points.Count == 0)
            {
                return "  (empty diagram)\n";
            }

            int minX = points.Min(p => p.X);
            int minY = points.Min(p => p.Y);
            int spanX = Math.Max(1, points.Max(p => p.X) - minX);
            int spanY = Math.Max(1, points.Max(p => p.Y) - minY);

            int MapX(int mx) => Math.Clamp((int)Math.Round((mx - minX) / (double)spanX * (width - 1)), 0, width - 1);
            int MapY(int my) => Math.Clamp((int)Math.Round((my - minY) / (double)spanY * (height - 1)), 0, height - 1);

            char[,] grid = new char[height, width];
            int[,] colors = new int[height, width];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    grid[y, x] = ' ';
                }
            }

            foreach (DrawingElement element in elements.Where(e => e is BorderBoundary))
            {
                int boundaryLeft = MapX(element.Left);
                int boundaryTop = MapY(element.Top);
                int boundaryRight = MapX(element.Left + element.Width);
                int boundaryBottom = MapY(element.Top + element.Height);
                DrawBox(grid, colors, boundaryLeft, boundaryTop, boundaryRight, boundaryBottom, unicode ? "┏┓┗┛━┃" : "++++-|", ColorBoundary);

                string boundaryName = DiagramElementHelper.GetName(element);
                int labelRoom = boundaryRight - boundaryLeft - 3;
                if (!string.IsNullOrEmpty(boundaryName) && labelRoom >= 1)
                {
                    string labelText = boundaryName.Length > labelRoom ? boundaryName.Substring(0, labelRoom) : boundaryName;
                    for (int i = 0; i < labelText.Length; i++)
                    {
                        Plot(grid, colors, boundaryLeft + 2 + i, boundaryTop, labelText[i], ColorBoundary);
                    }
                }
            }

            foreach (Connector connector in connectors)
            {
                int x0 = MapX(connector.SourceX);
                int y0 = MapY(connector.SourceY);
                int x1 = MapX(connector.TargetX);
                int y1 = MapY(connector.TargetY);
                int dx = x1 - x0;
                int dy = y1 - y0;
                char body = Math.Abs(dx) >= Math.Abs(dy) ? (unicode ? '─' : '-') : (unicode ? '│' : '|');
                DrawLine(grid, colors, x0, y0, x1, y1, body, ArrowChar(dx, dy, unicode), ColorFlow);
            }

            foreach (LineBoundary line in lineBoundaries)
            {
                char body = unicode ? '━' : '=';
                DrawLine(grid, colors, MapX(line.SourceX), MapY(line.SourceY), MapX(line.TargetX), MapY(line.TargetY), body, body, ColorBoundary);
            }

            List<BorderBoundary> boundaries = elements.OfType<BorderBoundary>().ToList();
            foreach (DrawingElement element in elements.Where(e => !(e is BorderBoundary)))
            {
                int kind = Classify(element);
                int centerX = MapX(element.Left + (element.Width / 2));
                int centerY = MapY(element.Top + (element.Height / 2));
                string name = DiagramElementHelper.GetName(element);

                // Keep the box (and its clipped label) inside the tightest enclosing boundary so a
                // long name doesn't overrun the boundary wall; fall back to the whole canvas when
                // the element is not inside a boundary.
                int limitLeft = 0;
                int limitRight = width - 1;
                int limitTop = 0;
                int limitBottom = height - 1;
                BorderBoundary? host = FindEnclosingBoundary(boundaries, element);
                if (host != null)
                {
                    limitLeft = Math.Min(MapX(host.Left) + 1, width - 1);
                    limitRight = Math.Max(MapX(host.Left + host.Width) - 1, limitLeft);
                    limitTop = Math.Min(MapY(host.Top) + 1, height - 1);
                    limitBottom = Math.Max(MapY(host.Top + host.Height) - 1, limitTop);
                }

                int maxBoxWidth = Math.Max(3, limitRight - limitLeft + 1);
                int boxWidth = Math.Min(maxBoxWidth, Math.Max(3, name.Length + 2));
                int half = boxWidth / 2;
                int left = centerX - half;
                int right = centerX + (boxWidth - 1 - half);
                (left, right) = ClampSpan(left, right, limitLeft, limitRight);
                int top = centerY - 1;
                int bottom = centerY + 1;
                (top, bottom) = ClampSpan(top, bottom, limitTop, limitBottom);
                int boxCenterX = (left + right) / 2;
                int boxCenterY = (top + bottom) / 2;
                int code = kind == Process ? ColorProcess : kind == DataStore ? ColorStore : ColorExternal;

                if (kind == DataStore)
                {
                    DrawStore(grid, colors, left, top, right, bottom, unicode ? '═' : '=', code);
                }
                else
                {
                    DrawBox(grid, colors, left, top, right, bottom, kind == Process ? (unicode ? "╭╮╰╯─│" : "++++-|") : (unicode ? "┌┐└┘─│" : "++++-|"), code);
                }

                DrawLabel(grid, colors, boxCenterX, boxCenterY, name, code, boxWidth - 2);
            }

            return Emit(grid, colors, width, height, color);
        }

        private static int Classify(DrawingElement element)
        {
            if (element is BorderBoundary)
            {
                return Boundary;
            }

            if (element is StencilEllipse)
            {
                return Process;
            }

            if (element is StencilParallelLines)
            {
                return DataStore;
            }

            return External;
        }

        private static char ArrowChar(int dx, int dy, bool unicode)
        {
            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                return dx >= 0 ? (unicode ? '►' : '>') : (unicode ? '◄' : '<');
            }

            return dy >= 0 ? (unicode ? '▼' : 'v') : (unicode ? '▲' : '^');
        }

        private static (int Start, int End) ClampSpan(int start, int end, int size) =>
            ClampSpan(start, end, 0, size - 1);

        private static (int Start, int End) ClampSpan(int start, int end, int lo, int hi)
        {
            if (start < lo)
            {
                end += lo - start;
                start = lo;
            }

            if (end > hi)
            {
                int shift = end - hi;
                start -= shift;
                end -= shift;
                if (start < lo)
                {
                    start = lo;
                }
            }

            return (start, end);
        }

        private static BorderBoundary? FindEnclosingBoundary(List<BorderBoundary> boundaries, DrawingElement element)
        {
            int centerX = element.Left + (element.Width / 2);
            int centerY = element.Top + (element.Height / 2);
            BorderBoundary? best = null;
            long bestArea = long.MaxValue;
            foreach (BorderBoundary boundary in boundaries)
            {
                if (ReferenceEquals(boundary, element))
                {
                    continue;
                }

                if (centerX < boundary.Left || centerX > boundary.Left + boundary.Width ||
                    centerY < boundary.Top || centerY > boundary.Top + boundary.Height)
                {
                    continue;
                }

                long area = (long)boundary.Width * boundary.Height;
                if (area < bestArea)
                {
                    bestArea = area;
                    best = boundary;
                }
            }

            return best;
        }

        private static void Plot(char[,] grid, int[,] colors, int x, int y, char ch, int code)
        {
            if (x >= 0 && y >= 0 && y < grid.GetLength(0) && x < grid.GetLength(1))
            {
                grid[y, x] = ch;
                colors[y, x] = code;
            }
        }

        private static void DrawBox(char[,] grid, int[,] colors, int left, int top, int right, int bottom, string style, int code)
        {
            if (right - left < 1 || bottom - top < 1)
            {
                Plot(grid, colors, left, top, style[0], code);
                return;
            }

            for (int x = left + 1; x < right; x++)
            {
                Plot(grid, colors, x, top, style[4], code);
                Plot(grid, colors, x, bottom, style[4], code);
            }

            for (int y = top + 1; y < bottom; y++)
            {
                Plot(grid, colors, left, y, style[5], code);
                Plot(grid, colors, right, y, style[5], code);
            }

            Plot(grid, colors, left, top, style[0], code);
            Plot(grid, colors, right, top, style[1], code);
            Plot(grid, colors, left, bottom, style[2], code);
            Plot(grid, colors, right, bottom, style[3], code);
        }

        private static void DrawStore(char[,] grid, int[,] colors, int left, int top, int right, int bottom, char horizontal, int code)
        {
            for (int x = left; x <= right; x++)
            {
                Plot(grid, colors, x, top, horizontal, code);
                Plot(grid, colors, x, bottom, horizontal, code);
            }
        }

        private static void DrawLine(char[,] grid, int[,] colors, int x0, int y0, int x1, int y1, char body, char arrow, int code)
        {
            List<(int X, int Y)> pts = BresenhamPoints(x0, y0, x1, y1);
            if (pts.Count == 0)
            {
                return;
            }

            for (int i = 0; i < pts.Count - 1; i++)
            {
                Plot(grid, colors, pts[i].X, pts[i].Y, body, code);
            }

            int arrowIndex = pts.Count >= 2 ? pts.Count - 2 : 0;
            Plot(grid, colors, pts[arrowIndex].X, pts[arrowIndex].Y, arrow, code);
        }

        private static List<(int X, int Y)> BresenhamPoints(int x0, int y0, int x1, int y1)
        {
            List<(int X, int Y)> result = new List<(int X, int Y)>();
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int stepX = x0 < x1 ? 1 : -1;
            int stepY = y0 < y1 ? 1 : -1;
            int error = dx - dy;
            int x = x0;
            int y = y0;
            while (true)
            {
                result.Add((x, y));
                if (x == x1 && y == y1)
                {
                    break;
                }

                int doubleError = 2 * error;
                if (doubleError > -dy)
                {
                    error -= dy;
                    x += stepX;
                }

                if (doubleError < dx)
                {
                    error += dx;
                    y += stepY;
                }
            }

            return result;
        }

        private static void DrawLabel(char[,] grid, int[,] colors, int centerX, int rowY, string text, int code, int maxWidth)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0)
            {
                return;
            }

            string label = text.Length > maxWidth ? text.Substring(0, maxWidth) : text;
            int startX = centerX - (label.Length / 2);
            for (int i = 0; i < label.Length; i++)
            {
                Plot(grid, colors, startX + i, rowY, label[i], code);
            }
        }

        private static string Emit(char[,] grid, int[,] colors, int width, int height, bool color)
        {
            int minRow = height;
            int maxRow = -1;
            int maxCol = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (grid[y, x] != ' ')
                    {
                        minRow = Math.Min(minRow, y);
                        maxRow = Math.Max(maxRow, y);
                        maxCol = Math.Max(maxCol, x);
                    }
                }
            }

            if (maxRow < 0)
            {
                return "  (empty diagram)\n";
            }

            StringBuilder builder = new StringBuilder();
            for (int y = minRow; y <= maxRow; y++)
            {
                int rowLast = -1;
                for (int x = 0; x <= maxCol; x++)
                {
                    if (grid[y, x] != ' ')
                    {
                        rowLast = x;
                    }
                }

                if (color)
                {
                    int current = 0;
                    for (int x = 0; x <= rowLast; x++)
                    {
                        int code = colors[y, x];
                        if (code != current)
                        {
                            builder.Append("\u001b[").Append(code == 0 ? "0" : code.ToString(CultureInfo.InvariantCulture)).Append('m');
                            current = code;
                        }

                        builder.Append(grid[y, x]);
                    }

                    if (current != 0)
                    {
                        builder.Append("\u001b[0m");
                    }
                }
                else
                {
                    for (int x = 0; x <= rowLast; x++)
                    {
                        builder.Append(grid[y, x]);
                    }
                }

                builder.Append('\n');
            }

            return builder.ToString();
        }
    }
}
