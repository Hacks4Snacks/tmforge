namespace ThreatModelForge.Formats
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Builds an editable Visio <c>.vsdx</c> by injecting a data-flow diagram into a known-good
    /// Visio template ("template injection"): a copy of the template has its pages replaced with
    /// one generated page per diagram, and the scaffolding (masters, theme, windows, thumbnail) is preserved.
    /// Connectors are real Dynamic-connector instances carrying explicit endpoint coordinate values
    /// (so Visio for the web, which does not run the router on load, renders them correctly on first
    /// open) alongside glue formulas (so Visio desktop keeps them live-glued when shapes move).
    /// Author coordinates are top-origin inches (y grows downward) and are converted to Visio's
    /// bottom-left origin on emit.
    /// </summary>
    internal sealed class VsdxDiagram
    {
        private const string PageNs = "http://schemas.microsoft.com/office/visio/2012/main";
        private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        private static readonly Dictionary<string, (string Fill, string Line)> Kinds =
            new Dictionary<string, (string, string)>(StringComparer.Ordinal)
            {
                ["proc"] = ("#DAE8FC", "#6C8EBF"),
                ["store"] = ("#FFF2CC", "#D6B656"),
                ["ext"] = ("#D5E8D4", "#82B366"),
                ["obs"] = ("#F5F5F5", "#999999"),
            };

        private readonly double width;
        private readonly double height;
        private readonly List<(string Name, double X, double Y, double W, double H, string Label, string Fill, string Line, bool Bold)> nodes = new List<(string, double, double, double, double, string, string, string, bool)>();
        private readonly Dictionary<string, int> nodeIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyList<(string Label, string Value)>> nodeShapeData = new Dictionary<string, IReadOnlyList<(string Label, string Value)>>(StringComparer.Ordinal);
        private readonly List<(double X0, double Y0, double X1, double Y1, string Label, string Color)> bounds = new List<(double, double, double, double, string, string)>();
        private readonly List<(double X0, double Y0, double Hx, double Hy, double X1, double Y1, string Label, string Color)> boundaryLines = new List<(double, double, double, double, double, double, string, string)>();
        private readonly List<(string Src, string Dst, string Label, bool Dashed, double Bow)> flows = new List<(string, string, string, bool, double)>();
        private readonly List<(double X, double Y, double W, string Label, double Size, string Color)> texts = new List<(double, double, double, string, double, string)>();

        /// <summary>
        /// Initializes a new instance of the <see cref="VsdxDiagram"/> class.
        /// </summary>
        /// <param name="width">The page width in inches.</param>
        /// <param name="height">The page height in inches.</param>
        internal VsdxDiagram(double width, double height)
        {
            this.width = width;
            this.height = height;
        }

        /// <summary>
        /// Packages the supplied diagrams as the pages of a copy of the template and returns the
        /// resulting <c>.vsdx</c> bytes: page 1 replaces the template's page, and each additional
        /// diagram is added as a new <c>visio/pages/pageN.xml</c> part (with its relationship,
        /// content-type override, and page rels), so a multi-page model round-trips one Visio page
        /// per diagram.
        /// </summary>
        /// <param name="templateBytes">The known-good Visio template package.</param>
        /// <param name="pages">The diagrams to emit, in order, each with its page (tab) name.</param>
        /// <returns>The generated <c>.vsdx</c> bytes.</returns>
        internal static byte[] ToVsdx(byte[] templateBytes, IReadOnlyList<(string Name, VsdxDiagram Page)> pages)
        {
            Dictionary<string, byte[]> entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            List<string> order = new List<string>();
            using (MemoryStream input = new MemoryStream(templateBytes, writable: false))
            using (ZipArchive zip = new ZipArchive(input, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    using MemoryStream buffer = new MemoryStream();
                    using (Stream entryStream = entry.Open())
                    {
                        entryStream.CopyTo(buffer);
                    }

                    entries[entry.FullName] = buffer.ToArray();
                    order.Add(entry.FullName);
                }
            }

            string masters = Encoding.UTF8.GetString(entries["visio/masters/masters.xml"]);
            string masterId = FindConnectorMaster(masters);
            string pagesXml = Encoding.UTF8.GetString(entries["visio/pages/pages.xml"]);
            string pagesRels = Encoding.UTF8.GetString(entries["visio/pages/_rels/pages.xml.rels"]);
            string firstPage = FirstPageFile(pagesXml, pagesRels);
            string firstPagePath = "visio/pages/" + firstPage;
            string firstPageRelsPath = "visio/pages/_rels/" + firstPage + ".rels";
            byte[] pageRels = entries.TryGetValue(firstPageRelsPath, out byte[] found) ? found : Array.Empty<byte>();

            Match block = Regex.Match(pagesXml, "<Page\\b.*?</Page>", RegexOptions.Singleline);
            string pagesHeader = pagesXml.Substring(0, block.Index);
            string pageBlockTemplate = block.Value;

            List<byte[]> pageContents = new List<byte[]>();
            StringBuilder pageBlocks = new StringBuilder();
            StringBuilder relBuilder = new StringBuilder();
            StringBuilder contentTypeOverrides = new StringBuilder();
            for (int i = 0; i < pages.Count; i++)
            {
                int number = i + 1;
                VsdxDiagram page = pages[i].Page;
                pageContents.Add(Encoding.UTF8.GetBytes(page.RenderPage(masterId)));
                pageBlocks.Append(CustomizePageBlock(pageBlockTemplate, i, pages[i].Name, page.width, page.height));
                relBuilder.Append("<Relationship Id=\"rId").Append(number.ToString(CultureInfo.InvariantCulture))
                    .Append("\" Type=\"http://schemas.microsoft.com/visio/2010/relationships/page\" Target=\"page")
                    .Append(number.ToString(CultureInfo.InvariantCulture)).Append(".xml\"/>");
                if (number >= 2)
                {
                    contentTypeOverrides.Append("<Override PartName=\"/visio/pages/page")
                        .Append(number.ToString(CultureInfo.InvariantCulture))
                        .Append(".xml\" ContentType=\"application/vnd.ms-visio.page+xml\"/>");
                }
            }

            byte[] newPages = Encoding.UTF8.GetBytes(pagesHeader + pageBlocks + "</Pages>");
            byte[] newPagesRels = Encoding.UTF8.GetBytes(
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + relBuilder + "</Relationships>");
            string contentTypes = Encoding.UTF8.GetString(entries["[Content_Types].xml"]);
            if (contentTypeOverrides.Length > 0)
            {
                contentTypes = contentTypes.Replace("</Types>", contentTypeOverrides + "</Types>");
            }

            using MemoryStream outputStream = new MemoryStream();
            using (ZipArchive output = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                WriteEntry(output, "[Content_Types].xml", Encoding.UTF8.GetBytes(contentTypes));
                foreach (string name in order)
                {
                    if (string.Equals(name, "[Content_Types].xml", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (string.Equals(name, "visio/pages/pages.xml", StringComparison.Ordinal))
                    {
                        WriteEntry(output, name, newPages);
                    }
                    else if (string.Equals(name, "visio/pages/_rels/pages.xml.rels", StringComparison.Ordinal))
                    {
                        WriteEntry(output, name, newPagesRels);
                    }
                    else if (string.Equals(name, firstPagePath, StringComparison.Ordinal))
                    {
                        WriteEntry(output, name, pageContents[0]);
                    }
                    else if (IsOtherPagePart(name, firstPage))
                    {
                        continue;
                    }
                    else
                    {
                        WriteEntry(output, name, entries[name]);
                    }
                }

                for (int i = 1; i < pages.Count; i++)
                {
                    int number = i + 1;
                    WriteEntry(output, "visio/pages/page" + number.ToString(CultureInfo.InvariantCulture) + ".xml", pageContents[i]);
                    if (pageRels.Length > 0)
                    {
                        WriteEntry(output, "visio/pages/_rels/page" + number.ToString(CultureInfo.InvariantCulture) + ".xml.rels", pageRels);
                    }
                }
            }

            return outputStream.ToArray();
        }

        /// <summary>
        /// Adds a node (rectangle) at the given center, top-origin inches.
        /// </summary>
        /// <param name="name">The unique node handle referenced by flows.</param>
        /// <param name="centerX">The center x, in inches.</param>
        /// <param name="centerY">The center y, in inches (top-origin).</param>
        /// <param name="w">The width, in inches.</param>
        /// <param name="h">The height, in inches.</param>
        /// <param name="label">The display label.</param>
        /// <param name="kind">The DFD kind: <c>proc</c>, <c>store</c>, <c>ext</c>, or <c>obs</c>.</param>
        /// <param name="shapeData">Per-shape Visio Shape Data rows (label/value), for example custom
        /// properties and associated threats. May be empty.</param>
        internal void AddNode(string name, double centerX, double centerY, double w, double h, string label, string kind, IReadOnlyList<(string Label, string Value)> shapeData)
        {
            (string fill, string line) = Kinds.TryGetValue(kind, out (string Fill, string Line) k) ? k : Kinds["proc"];
            this.nodeIndex[name] = this.nodes.Count;
            this.nodes.Add((name, centerX, centerY, w, h, label ?? string.Empty, fill, line, kind == "proc"));
            this.nodeShapeData[name] = shapeData ?? Array.Empty<(string, string)>();
        }

        /// <summary>
        /// Adds a dashed trust-boundary rectangle, top-origin inches.
        /// </summary>
        /// <param name="x0">The left edge, in inches.</param>
        /// <param name="y0">The top edge, in inches.</param>
        /// <param name="x1">The right edge, in inches.</param>
        /// <param name="y1">The bottom edge, in inches.</param>
        /// <param name="label">The boundary label.</param>
        internal void AddBoundary(double x0, double y0, double x1, double y1, string label)
        {
            this.bounds.Add((x0, y0, x1, y1, label ?? string.Empty, "#B85450"));
        }

        /// <summary>
        /// Adds a dashed trust-boundary line that follows the quadratic curve from its start through
        /// its control handle to its end, top-origin inches. A handle at the chord midpoint yields a
        /// straight line.
        /// </summary>
        /// <param name="x0">The start x, in inches.</param>
        /// <param name="y0">The start y, in inches (top-origin).</param>
        /// <param name="hx">The control-handle x, in inches.</param>
        /// <param name="hy">The control-handle y, in inches (top-origin).</param>
        /// <param name="x1">The end x, in inches.</param>
        /// <param name="y1">The end y, in inches (top-origin).</param>
        /// <param name="label">The boundary label.</param>
        internal void AddBoundaryLine(double x0, double y0, double hx, double hy, double x1, double y1, string label)
        {
            this.boundaryLines.Add((x0, y0, hx, hy, x1, y1, label ?? string.Empty, "#B85450"));
        }

        /// <summary>
        /// Adds a data flow connecting two nodes by name.
        /// </summary>
        /// <param name="src">The source node handle.</param>
        /// <param name="dst">The target node handle.</param>
        /// <param name="label">The flow label.</param>
        /// <param name="dashed">Whether the flow is dashed (for example, out-of-scope).</param>
        /// <param name="bow">A perpendicular offset (inches) to separate parallel/bidirectional pairs.</param>
        internal void AddFlow(string src, string dst, string label, bool dashed, double bow)
        {
            this.flows.Add((src, dst, label ?? string.Empty, dashed, bow));
        }

        /// <summary>
        /// Adds free text (a title or annotation), top-origin inches.
        /// </summary>
        /// <param name="centerX">The center x, in inches.</param>
        /// <param name="centerY">The center y, in inches (top-origin).</param>
        /// <param name="w">The width, in inches.</param>
        /// <param name="label">The text.</param>
        /// <param name="size">The font size, in inches (points / 72).</param>
        internal void AddText(double centerX, double centerY, double w, string label, double size)
        {
            this.texts.Add((centerX, centerY, w, label ?? string.Empty, size, "#1F3864"));
        }

        private static string CustomizePageBlock(string template, int index, string name, double width, double height)
        {
            string esc = AttrEsc(name);
            string block = new Regex("ID='0'").Replace(template, "ID='" + index.ToString(CultureInfo.InvariantCulture) + "'", 1);
            block = block.Replace(
                "NameU='Page-1' Name='Page-1'",
                "NameU='" + esc + "' Name='" + esc + "' IsCustomName='1' IsCustomNameU='1'");
            block = new Regex("(<Cell N='PageWidth' V=')[^']*'").Replace(block, "$1" + width.ToString(CultureInfo.InvariantCulture) + "'", 1);
            block = new Regex("(<Cell N='PageHeight' V=')[^']*'").Replace(block, "$1" + height.ToString(CultureInfo.InvariantCulture) + "'", 1);
            block = new Regex("ViewCenterX='[^']*'").Replace(block, "ViewCenterX='" + S(width / 2) + "'", 1);
            block = new Regex("ViewCenterY='[^']*'").Replace(block, "ViewCenterY='" + S(height / 2) + "'", 1);
            return block.Replace("<Rel r:id='rId1'/>", "<Rel r:id='rId" + (index + 1).ToString(CultureInfo.InvariantCulture) + "'/>");
        }

        private static string AttrEsc(string s) =>
            (s ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("'", "&apos;").Replace("\"", "&quot;");

        private static string Esc(string s) =>
            (s ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        private static string S(double v) => v.ToString("0.0000", CultureInfo.InvariantCulture);

        private static string CharSection(double size, string color, bool bold)
        {
            int style = bold ? 1 : 0;
            return $"<Section N='Character'><Row IX='0'><Cell N='Font' V='Calibri'/>"
                + $"<Cell N='Size' V='{S(size)}'/><Cell N='Color' V='{color}'/>"
                + $"<Cell N='Style' V='{style.ToString(CultureInfo.InvariantCulture)}'/></Row></Section>";
        }

        private static IReadOnlyList<(int Ix, double Lx, double Ly)> ConnPoints(double w, double h)
        {
            List<(int, double, double)> points = new List<(int, double, double)>();
            int ix = 0;
            foreach (double fx in new[] { 0.25, 0.5, 0.75 })
            {
                points.Add((ix++, fx * w, h));
            }

            foreach (double fx in new[] { 0.25, 0.5, 0.75 })
            {
                points.Add((ix++, fx * w, 0.0));
            }

            foreach (double fy in new[] { 0.75, 0.5, 0.25 })
            {
                points.Add((ix++, 0.0, fy * h));
            }

            foreach (double fy in new[] { 0.75, 0.5, 0.25 })
            {
                points.Add((ix++, w, fy * h));
            }

            return points;
        }

        private static string ConnSection(double w, double h)
        {
            StringBuilder rows = new StringBuilder();
            foreach ((int ix, double lx, double ly) in ConnPoints(w, h))
            {
                rows.Append($"<Row T='Connection' IX='{ix.ToString(CultureInfo.InvariantCulture)}'>")
                    .Append($"<Cell N='X' V='{S(lx)}'/><Cell N='Y' V='{S(ly)}'/></Row>");
            }

            return $"<Section N='Connection'>{rows}</Section>";
        }

        private static string PropertySection(IReadOnlyList<(string Label, string Value)> data)
        {
            if (data == null || data.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder rows = new StringBuilder();
            for (int i = 0; i < data.Count; i++)
            {
                rows.Append($"<Row N='Prop{(i + 1).ToString(CultureInfo.InvariantCulture)}'>")
                    .Append($"<Cell N='Label' V='{Esc(data[i].Label)}'/>")
                    .Append($"<Cell N='Value' V='{Esc(data[i].Value)}'/>")
                    .Append("<Cell N='Type' V='0'/></Row>");
            }

            return $"<Section N='Property'>{rows}</Section>";
        }

        private static string Box(
            int sid,
            double cx,
            double cyv,
            double w,
            double h,
            string fill,
            string line,
            string text,
            int fillPattern,
            int linePattern,
            double lineWeight,
            double size,
            bool bold,
            string textColor,
            int valign,
            string conn)
        {
            string para = valign == 0
                ? "<Section N='Paragraph'><Row IX='0'><Cell N='HorzAlign' V='0'/></Row></Section>"
                : string.Empty;
            string eventCell = string.IsNullOrEmpty(conn) ? string.Empty : "<Cell N='EventXFMod' V='0'/>";
            string geometry =
                "<Section N='Geometry' IX='0'>"
                + "<Row T='MoveTo' IX='1'><Cell N='X' V='0'/><Cell N='Y' V='0'/></Row>"
                + $"<Row T='LineTo' IX='2'><Cell N='X' V='{S(w)}'/><Cell N='Y' V='0'/></Row>"
                + $"<Row T='LineTo' IX='3'><Cell N='X' V='{S(w)}'/><Cell N='Y' V='{S(h)}'/></Row>"
                + $"<Row T='LineTo' IX='4'><Cell N='X' V='0'/><Cell N='Y' V='{S(h)}'/></Row>"
                + "<Row T='LineTo' IX='5'><Cell N='X' V='0'/><Cell N='Y' V='0'/></Row></Section>";

            return $"<Shape ID='{sid.ToString(CultureInfo.InvariantCulture)}' Type='Shape'>"
                + $"<Cell N='PinX' V='{S(cx)}'/><Cell N='PinY' V='{S(cyv)}'/>"
                + $"<Cell N='Width' V='{S(w)}'/><Cell N='Height' V='{S(h)}'/>"
                + $"<Cell N='LocPinX' V='{S(w / 2)}'/><Cell N='LocPinY' V='{S(h / 2)}'/>"
                + "<Cell N='Angle' V='0'/>"
                + $"<Cell N='FillForegnd' V='{fill}'/><Cell N='FillPattern' V='{fillPattern.ToString(CultureInfo.InvariantCulture)}'/>"
                + $"<Cell N='LineColor' V='{line}'/><Cell N='LinePattern' V='{linePattern.ToString(CultureInfo.InvariantCulture)}'/>"
                + $"<Cell N='LineWeight' V='{S(lineWeight)}'/><Cell N='Rounding' V='0'/>"
                + $"<Cell N='VerticalAlign' V='{valign.ToString(CultureInfo.InvariantCulture)}'/>"
                + $"{eventCell}{geometry}{conn}{CharSection(size, textColor, bold)}{para}"
                + $"<Text><cp IX='0'/>{Esc(text)}</Text></Shape>";
        }

        private static string Connector(
            int sid,
            string masterId,
            double bxv,
            double byv,
            double exv,
            double eyv,
            string label,
            bool dashed,
            int srcId,
            int bix,
            int dstId,
            int eix)
        {
            string color = dashed ? "#999999" : "#405060";
            int lpat = dashed ? 2 : 1;
            double pinx = (bxv + exv) / 2;
            double piny = (byv + eyv) / 2;
            double w = exv - bxv;
            double h = eyv - byv;
            string bg = $"PAR(PNT(Sheet.{srcId.ToString(CultureInfo.InvariantCulture)}!Connections.X{(bix + 1).ToString(CultureInfo.InvariantCulture)},"
                + $"Sheet.{srcId.ToString(CultureInfo.InvariantCulture)}!Connections.Y{(bix + 1).ToString(CultureInfo.InvariantCulture)}))";
            string en = $"PAR(PNT(Sheet.{dstId.ToString(CultureInfo.InvariantCulture)}!Connections.X{(eix + 1).ToString(CultureInfo.InvariantCulture)},"
                + $"Sheet.{dstId.ToString(CultureInfo.InvariantCulture)}!Connections.Y{(eix + 1).ToString(CultureInfo.InvariantCulture)}))";

            return $"<Shape ID='{sid.ToString(CultureInfo.InvariantCulture)}' Type='Shape' Master='{masterId}'>"
                + $"<Cell N='PinX' V='{S(pinx)}' F='GUARD((BeginX+EndX)/2)'/>"
                + $"<Cell N='PinY' V='{S(piny)}' F='GUARD((BeginY+EndY)/2)'/>"
                + $"<Cell N='Width' V='{S(w)}' F='GUARD(EndX-BeginX)'/>"
                + $"<Cell N='Height' V='{S(h)}' F='GUARD(EndY-BeginY)'/>"
                + $"<Cell N='LocPinX' V='{S(w / 2)}' F='GUARD(Width*0.5)'/>"
                + $"<Cell N='LocPinY' V='{S(h / 2)}' F='GUARD(Height*0.5)'/>"
                + $"<Cell N='BeginX' V='{S(bxv)}' F='{bg}'/>"
                + $"<Cell N='BeginY' V='{S(byv)}' F='{bg}'/>"
                + $"<Cell N='EndX' V='{S(exv)}' F='{en}'/>"
                + $"<Cell N='EndY' V='{S(eyv)}' F='{en}'/>"
                + $"<Cell N='BegTrigger' V='2' F='_XFTRIGGER(Sheet.{srcId.ToString(CultureInfo.InvariantCulture)}!EventXFMod)'/>"
                + $"<Cell N='EndTrigger' V='2' F='_XFTRIGGER(Sheet.{dstId.ToString(CultureInfo.InvariantCulture)}!EventXFMod)'/>"
                + "<Cell N='ShapeRouteStyle' V='1'/><Cell N='LayerMember' V='0'/>"
                + "<Cell N='BeginArrow' V='0'/><Cell N='EndArrow' V='4'/>"
                + $"<Cell N='LineColor' V='{color}'/><Cell N='LinePattern' V='{lpat.ToString(CultureInfo.InvariantCulture)}'/>"
                + "<Cell N='LineWeight' V='0.0125'/>"
                + "<Section N='Geometry' IX='0'><Cell N='NoFill' V='1'/><Cell N='NoLine' V='0'/>"
                + "<Row T='MoveTo' IX='1'><Cell N='X' V='0' F='Width*0'/><Cell N='Y' V='0' F='Height*0'/></Row>"
                + $"<Row T='LineTo' IX='2'><Cell N='X' V='{S(w)}' F='Width*1'/><Cell N='Y' V='{S(h)}' F='Height*1'/></Row></Section>"
                + $"{CharSection(0.10, "#203040", false)}"
                + $"<Text><cp IX='0'/>{Esc(label)}</Text></Shape>";
        }

        private static (double X, double Y) Border(double cx, double cy, double w, double h, double tx, double ty, double gap)
        {
            double dx = tx - cx;
            double dy = ty - cy;
            if (dx == 0 && dy == 0)
            {
                return (cx, cy);
            }

            double tX = dx != 0 ? (w / 2) / Math.Abs(dx) : double.PositiveInfinity;
            double tY = dy != 0 ? (h / 2) / Math.Abs(dy) : double.PositiveInfinity;
            double t = Math.Min(tX, tY);
            double px = cx + (dx * t);
            double py = cy + (dy * t);
            double len = Math.Sqrt((dx * dx) + (dy * dy));
            return (px + (dx / len * gap), py + (dy / len * gap));
        }

        private static IReadOnlyList<(double X, double Y)> QuadTessellate(double x0, double y0, double cx, double cy, double x1, double y1)
        {
            double mx = (x0 + x1) / 2;
            double my = (y0 + y1) / 2;
            if (Math.Abs(cx - mx) < 0.001 && Math.Abs(cy - my) < 0.001)
            {
                return new[] { (x0, y0), (x1, y1) };
            }

            const int segments = 24;
            List<(double X, double Y)> points = new List<(double X, double Y)>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                double t = (double)i / segments;
                double u = 1.0 - t;
                points.Add((
                    (u * u * x0) + (2 * u * t * cx) + (t * t * x1),
                    (u * u * y0) + (2 * u * t * cy) + (t * t * y1)));
            }

            return points;
        }

        private static string BoundaryCurve(int sid, double x0, double y0, double cx, double cy, double x1, double y1, string label, string color)
        {
            IReadOnlyList<(double X, double Y)> points = QuadTessellate(x0, y0, cx, cy, x1, y1);
            double minX = points[0].X;
            double minY = points[0].Y;
            double maxX = points[0].X;
            double maxY = points[0].Y;
            foreach ((double px, double py) in points)
            {
                minX = Math.Min(minX, px);
                minY = Math.Min(minY, py);
                maxX = Math.Max(maxX, px);
                maxY = Math.Max(maxY, py);
            }

            double w = Math.Max(maxX - minX, 0.01);
            double h = Math.Max(maxY - minY, 0.01);
            StringBuilder geometry = new StringBuilder("<Section N='Geometry' IX='0'><Cell N='NoFill' V='1'/><Cell N='NoLine' V='0'/>");
            for (int i = 0; i < points.Count; i++)
            {
                geometry.Append($"<Row T='{(i == 0 ? "MoveTo" : "LineTo")}' IX='{(i + 1).ToString(CultureInfo.InvariantCulture)}'>")
                    .Append($"<Cell N='X' V='{S(points[i].X - minX)}'/><Cell N='Y' V='{S(points[i].Y - minY)}'/></Row>");
            }

            geometry.Append("</Section>");

            return $"<Shape ID='{sid.ToString(CultureInfo.InvariantCulture)}' Type='Shape'>"
                + $"<Cell N='PinX' V='{S((minX + maxX) / 2)}'/><Cell N='PinY' V='{S((minY + maxY) / 2)}'/>"
                + $"<Cell N='Width' V='{S(w)}'/><Cell N='Height' V='{S(h)}'/>"
                + $"<Cell N='LocPinX' V='{S(w / 2)}'/><Cell N='LocPinY' V='{S(h / 2)}'/>"
                + "<Cell N='Angle' V='0'/>"
                + $"<Cell N='LineColor' V='{color}'/><Cell N='LinePattern' V='2'/><Cell N='LineWeight' V='0.02'/>"
                + $"{geometry}{CharSection(0.13, color, true)}"
                + $"<Text><cp IX='0'/>{Esc(label)}</Text></Shape>";
        }

        private static string FindConnectorMaster(string mastersXml)
        {
            foreach (Match block in Regex.Matches(mastersXml, "<Master\\b[^>]*>"))
            {
                Match nameU = Regex.Match(block.Value, "NameU='([^']*)'");
                Match id = Regex.Match(block.Value, "ID='(\\d+)'");
                if (nameU.Success && id.Success
                    && string.Equals(nameU.Groups[1].Value.Trim(), "dynamic connector", StringComparison.OrdinalIgnoreCase))
                {
                    return id.Groups[1].Value;
                }
            }

            throw new InvalidOperationException("Template has no 'Dynamic connector' master.");
        }

        private static bool IsOtherPagePart(string name, string firstPage)
        {
            Match page = Regex.Match(name, "^visio/pages/page(\\d+)\\.xml$");
            if (page.Success)
            {
                return !string.Equals(name, "visio/pages/" + firstPage, StringComparison.Ordinal);
            }

            Match rels = Regex.Match(name, "^visio/pages/_rels/page(\\d+)\\.xml\\.rels$");
            if (rels.Success)
            {
                return !string.Equals(name, "visio/pages/_rels/" + firstPage + ".rels", StringComparison.Ordinal);
            }

            return false;
        }

        private static void WriteEntry(ZipArchive archive, string name, byte[] data)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using Stream stream = entry.Open();
            stream.Write(data, 0, data.Length);
        }

        private static string FirstPageFile(string pagesXml, string pagesRels)
        {
            Match firstPage = Regex.Match(pagesXml, "<Page\\b.*?</Page>", RegexOptions.Singleline);
            Match rel = Regex.Match(firstPage.Value, "<Rel\\s+r:id=['\"]([^'\"]+)['\"]");
            string relId = rel.Groups[1].Value;

            foreach (Match relationship in Regex.Matches(pagesRels, "<Relationship\\b[^>]*>"))
            {
                Match id = Regex.Match(relationship.Value, "Id=['\"]([^'\"]+)['\"]");
                Match target = Regex.Match(relationship.Value, "Target=['\"]([^'\"]+)['\"]");
                if (id.Success && target.Success && string.Equals(id.Groups[1].Value, relId, StringComparison.Ordinal))
                {
                    return target.Groups[1].Value;
                }
            }

            throw new InvalidOperationException("Could not resolve the first page file from pages.xml.rels.");
        }

        private string RenderPage(string masterId)
        {
            List<string> parts = new List<string>();
            List<(int Cid, string From, int Bix, string To, int Eix)> connects = new List<(int, string, int, string, int)>();
            Dictionary<string, int> ids = new Dictionary<string, int>(StringComparer.Ordinal);
            int counter = 0;

            int New() => ++counter;
            double Yv(double v) => this.height - v;

            foreach ((string name, _, _, _, _, _, _, _, _) in this.nodes)
            {
                ids[name] = New();
            }

            foreach ((double x0, double y0, double x1, double y1, string label, string color) in this.bounds)
            {
                double cx = (x0 + x1) / 2;
                double cy = (y0 + y1) / 2;
                parts.Add(Box(New(), cx, Yv(cy), x1 - x0, y1 - y0, "#FFFFFF", color, label, 0, 2, 0.02, 0.15, true, color, 0, string.Empty));
            }

            foreach ((double x0, double y0, double hx, double hy, double x1, double y1, string label, string color) in this.boundaryLines)
            {
                parts.Add(BoundaryCurve(New(), x0, Yv(y0), hx, Yv(hy), x1, Yv(y1), label, color));
            }

            foreach ((string src, string dst, string label, bool dashed, double bow) in this.flows)
            {
                (_, double sx, double sy, double sw, double sh, _, _, _, _) = this.nodes[this.nodeIndex[src]];
                (_, double tx, double ty, double tw, double th, _, _, _, _) = this.nodes[this.nodeIndex[dst]];

                (double bx, double by) = Border(sx, sy, sw, sh, tx, ty, 0.04);
                (double ex, double ey) = Border(tx, ty, tw, th, sx, sy, 0.06);
                if (bow != 0)
                {
                    double dx = ex - bx;
                    double dy = ey - by;
                    double len = Math.Sqrt((dx * dx) + (dy * dy));
                    if (len == 0)
                    {
                        len = 1.0;
                    }

                    double nx = -dy / len;
                    double ny = dx / len;
                    bx += nx * bow;
                    by += ny * bow;
                    ex += nx * bow;
                    ey += ny * bow;
                }

                (int bix, double bxv, double byv) = this.Snap(sx, Yv(sy), sw, sh, bx, Yv(by));
                (int eix, double exv, double eyv) = this.Snap(tx, Yv(ty), tw, th, ex, Yv(ey));
                int sid = New();
                parts.Add(Connector(sid, masterId, bxv, byv, exv, eyv, label, dashed, ids[src], bix, ids[dst], eix));
                connects.Add((sid, src, bix, dst, eix));
            }

            foreach ((string name, double x, double y, double w, double h, string label, string fill, string line, bool bold) in this.nodes)
            {
                string sections = ConnSection(w, h)
                    + PropertySection(this.nodeShapeData.TryGetValue(name, out IReadOnlyList<(string Label, string Value)>? data) ? data : Array.Empty<(string, string)>());
                parts.Add(Box(ids[name], x, Yv(y), w, h, fill, line, label, 1, 1, 0.0104, 0.115, bold, "#000000", 1, sections));
            }

            foreach ((double x, double y, double w, string label, double size, string color) in this.texts)
            {
                parts.Add(Box(New(), x, Yv(y), w, 0.5, "#FFFFFF", "#FFFFFF", label, 0, 0, 0.0104, size, true, color, 1, string.Empty));
            }

            StringBuilder conn = new StringBuilder();
            foreach ((int cid, string from, int bix, string to, int eix) in connects)
            {
                conn.Append($"<Connect FromSheet='{cid.ToString(CultureInfo.InvariantCulture)}' FromCell='BeginX' FromPart='9' ")
                    .Append($"ToSheet='{ids[from].ToString(CultureInfo.InvariantCulture)}' ToCell='Connections.X{(bix + 1).ToString(CultureInfo.InvariantCulture)}' ")
                    .Append($"ToPart='{(100 + bix).ToString(CultureInfo.InvariantCulture)}'/>");
                conn.Append($"<Connect FromSheet='{cid.ToString(CultureInfo.InvariantCulture)}' FromCell='EndX' FromPart='12' ")
                    .Append($"ToSheet='{ids[to].ToString(CultureInfo.InvariantCulture)}' ToCell='Connections.X{(eix + 1).ToString(CultureInfo.InvariantCulture)}' ")
                    .Append($"ToPart='{(100 + eix).ToString(CultureInfo.InvariantCulture)}'/>");
            }

            return "<?xml version='1.0' encoding='utf-8' ?>\n"
                + $"<PageContents xmlns='{PageNs}' xmlns:r='{RelNs}' xml:space='preserve'>"
                + "<Shapes>" + string.Concat(parts) + "</Shapes>"
                + "<Connects>" + conn + "</Connects></PageContents>";
        }

        private (int Ix, double X, double Y) Snap(double cx, double cyv, double w, double h, double txv, double tyv)
        {
            int bestIx = 0;
            double bestX = cx;
            double bestY = cyv;
            double best = double.MaxValue;
            foreach ((int ix, double lx, double ly) in ConnPoints(w, h))
            {
                double px = cx - (w / 2) + lx;
                double py = cyv - (h / 2) + ly;
                double dd = ((px - txv) * (px - txv)) + ((py - tyv) * (py - tyv));
                if (dd < best)
                {
                    best = dd;
                    bestIx = ix;
                    bestX = px;
                    bestY = py;
                }
            }

            return (bestIx, bestX, bestY);
        }
    }
}
