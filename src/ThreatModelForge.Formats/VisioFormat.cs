namespace ThreatModelForge.Formats
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Reflection;
    using System.Xml.Linq;
    using ThreatModelForge.Editing;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// The Microsoft Visio (<c>.vsdx</c>) format provider. Exports the canonical model as an editable
    /// Visio drawing (via template injection, see <see cref="VsdxDiagram"/>) that opens in Visio
    /// desktop and Visio for the web, and imports the structural diagram back (nodes, flows, trust
    /// boundaries, names, geometry) from a Visio package this provider wrote, reconstructing the
    /// model via the UI-agnostic <see cref="DiagramEditor"/>. Element custom properties and the
    /// threats associated with each element are written as per-shape Visio Shape Data (a
    /// <c>Property</c> section, visible in Visio's Shape Data pane) and re-imported as custom
    /// properties. The mapping is structural, not lossless (<c>RoundTrips=false</c>): the rich threat
    /// model (<see cref="ThreatModel.AllThreatsDictionary"/>) is not itself reconstructed.
    /// </summary>
    public sealed class VisioFormat : IThreatModelFormat
    {
        /// <summary>
        /// The stable format identifier.
        /// </summary>
        public const string FormatId = "vsdx";

        private const string TemplateResource = "ThreatModelForge.Formats.template.vsdx";
        private const double TargetPageWidthInches = 15.0;
        private const double MarginInches = 0.6;
        private const double PixelsPerInch = 96.0;
        private const double DefaultPageHeightInches = 8.5;

        private static readonly IReadOnlyList<string> SupportedExtensions = new[] { ".vsdx" };

        private static readonly FormatCapabilities VisioCapabilities = new FormatCapabilities(
            canRead: true,
            canWrite: true,
            roundTrips: false,
            fidelityNote: "Editable Visio (.vsdx) via template injection. Structure (nodes, flows, trust boundaries, names, geometry) is preserved; element custom properties and associated threats are written as per-shape Visio Shape Data and re-imported as custom properties. Import recognizes packages this provider wrote (and the documented master/Shape convention).");

        private static readonly IReadOnlyList<string> BoundaryKeywords = new[]
        {
            "boundary", "trust", "dmz", "perimeter", "zone", "subnet", "vnet", "vpc",
        };

        private static readonly IReadOnlyList<string> DataStoreKeywords = new[]
        {
            "database", "datastore", "data store", "storage", "store", "queue", "bucket",
            "blob", "cache", "table", "disk", "volume", "sql", "redis", "kafka", "s3",
            "repository", "data lake", "warehouse",
        };

        private static readonly IReadOnlyList<string> ExternalKeywords = new[]
        {
            "user", "actor", "person", "people", "client", "browser", "customer",
            "external", "third party", "third-party", "3rd party", "mobile", "device",
            "internet", "partner", "admin", "operator", "consumer",
        };

        /// <inheritdoc/>
        public string Id => FormatId;

        /// <inheritdoc/>
        public string DisplayName => "Microsoft Visio (.vsdx)";

        /// <inheritdoc/>
        public IReadOnlyList<string> Extensions => SupportedExtensions;

        /// <inheritdoc/>
        public FormatCapabilities Capabilities => VisioCapabilities;

        /// <inheritdoc/>
        public bool CanRead(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!stream.CanSeek)
            {
                throw new NotSupportedException("Content sniffing requires a seekable stream.");
            }

            long originalPosition = stream.Position;
            try
            {
                using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
                {
                    return archive.GetEntry("visio/document.xml") != null;
                }
            }
            catch (InvalidDataException)
            {
                // Not an OPC zip package.
                return false;
            }
            finally
            {
                stream.Position = originalPosition;
            }
        }

        /// <inheritdoc/>
        public ThreatModel Read(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            string pageXml;
            string pagesXml;
            string mastersXml;
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
            {
                ZipArchiveEntry? pageEntry = archive.GetEntry("visio/pages/page1.xml")
                    ?? archive.Entries.FirstOrDefault(e =>
                        e.FullName.StartsWith("visio/pages/page", StringComparison.Ordinal)
                        && e.FullName.EndsWith(".xml", StringComparison.Ordinal)
                        && !e.FullName.EndsWith("pages.xml", StringComparison.Ordinal));
                if (pageEntry == null)
                {
                    throw new InvalidDataException("The Visio package has no page content part.");
                }

                pageXml = ReadEntry(pageEntry);
                ZipArchiveEntry? pagesEntry = archive.GetEntry("visio/pages/pages.xml");
                pagesXml = pagesEntry != null ? ReadEntry(pagesEntry) : string.Empty;
                ZipArchiveEntry? mastersEntry = archive.GetEntry("visio/masters/masters.xml");
                mastersXml = mastersEntry != null ? ReadEntry(mastersEntry) : string.Empty;
            }

            double pageHeight = ReadPageHeight(pagesXml);
            IReadOnlyDictionary<int, string> masterNames = ParseMasters(mastersXml);

            ThreatModel model = new ThreatModel { Version = "1.0" };
            DrawingSurfaceModel surface = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "Diagram 1" };
            model.DrawingSurfaceList.Add(surface);
            DiagramEditor editor = new DiagramEditor(model);

            XDocument page = XDocument.Parse(pageXml);
            (Dictionary<int, int> connectorSource, Dictionary<int, int> connectorTarget) = ParseConnects(page);
            HashSet<int> connectorIds = new HashSet<int>(connectorSource.Keys);
            connectorIds.UnionWith(connectorTarget.Keys);
            HashSet<int> connectedShapes = new HashSet<int>(connectorSource.Values);
            connectedShapes.UnionWith(connectorTarget.Values);

            Dictionary<int, Guid> nodeGuids = new Dictionary<int, Guid>();
            Dictionary<int, string> connectorLabels = new Dictionary<int, string>();
            string pageTitle = string.Empty;

            foreach (XElement shape in page.Descendants().Where(e => e.Name.LocalName == "Shape"))
            {
                int id = ParseInt(shape.Attribute("ID")?.Value);
                string text = (shape.Elements().FirstOrDefault(e => e.Name.LocalName == "Text")?.Value ?? string.Empty).Trim();
                Dictionary<string, string> cells = TopLevelCells(shape);
                string masterName = masterNames.TryGetValue(ParseInt(shape.Attribute("Master")?.Value), out string? resolvedMaster)
                    ? resolvedMaster
                    : string.Empty;

                if (IsConnector(id, cells, masterName, connectorIds))
                {
                    connectorLabels[id] = text;
                    continue;
                }

                StencilKind? kind = ClassifyShape(cells, masterName, text, connectedShapes.Contains(id));
                if (kind == null)
                {
                    // A title or annotation (no master, unconnected, no kind markers) is not a model
                    // element; the first such text is reused as the diagram header.
                    if (string.IsNullOrEmpty(pageTitle) && !string.IsNullOrEmpty(text))
                    {
                        pageTitle = text;
                    }

                    continue;
                }

                (int left, int top, int width, int height) = NodeBounds(cells, pageHeight);
                Guid guid = editor.AddElement(surface, kind.Value, left, top);
                if (width > 0 && height > 0)
                {
                    editor.ResizeElement(surface, guid, left, top, width, height);
                }

                editor.SetElementName(surface, guid, text);
                ApplyShapeData(surface, guid, ReadShapeData(shape));
                if (kind.Value != StencilKind.TrustBoundary)
                {
                    nodeGuids[id] = guid;
                }
            }

            if (!string.IsNullOrEmpty(pageTitle))
            {
                surface.Header = pageTitle;
            }

            BuildConnectors(editor, surface, connectorSource, connectorTarget, nodeGuids, connectorLabels);
            return model;
        }

        /// <inheritdoc/>
        public void Write(ThreatModel model, Stream stream)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            VsdxDiagram diagram = BuildDiagram(model, SelectSurface(model));
            byte[] vsdx = diagram.ToVsdx(LoadTemplate());
            stream.Write(vsdx, 0, vsdx.Length);
        }

        private static DrawingSurfaceModel SelectSurface(ThreatModel model)
        {
            for (int i = 0; i < model.DrawingSurfaceList.Count; i++)
            {
                if (model.DrawingSurfaceList[i].Borders.Count > 0)
                {
                    return model.DrawingSurfaceList[i];
                }
            }

            return model.DrawingSurfaceList.Count > 0 ? model.DrawingSurfaceList[0] : new DrawingSurfaceModel();
        }

        private static VsdxDiagram BuildDiagram(ThreatModel model, DrawingSurfaceModel surface)
        {
            List<double> xs = new List<double>();
            List<double> ys = new List<double>();
            foreach (DrawingElement e in surface.Borders.Values.OfType<DrawingElement>())
            {
                xs.Add(e.Left);
                xs.Add(e.Left + e.Width);
                ys.Add(e.Top);
                ys.Add(e.Top + e.Height);
            }

            foreach (LineElement l in surface.Lines.Values.OfType<LineElement>())
            {
                xs.Add(l.SourceX);
                xs.Add(l.TargetX);
                ys.Add(l.SourceY);
                ys.Add(l.TargetY);
                if (l.HandleX != 0 || l.HandleY != 0)
                {
                    xs.Add(l.HandleX);
                    ys.Add(l.HandleY);
                }
            }

            double minX = xs.Count > 0 ? xs.Min() : 0;
            double minY = ys.Count > 0 ? ys.Min() : 0;
            double maxX = xs.Count > 0 ? xs.Max() : 100;
            double maxY = ys.Count > 0 ? ys.Max() : 100;
            double scale = Math.Max((maxX - minX) / TargetPageWidthInches, 1.0);

            double Ix(double px) => ((px - minX) / scale) + MarginInches;
            double Iy(double px) => ((px - minY) / scale) + MarginInches;
            double Sz(double px) => px / scale;

            double pageW = Math.Round(((maxX - minX) / scale) + (2 * MarginInches), 2);
            double pageH = Math.Round(((maxY - minY) / scale) + (2 * MarginInches), 2);
            VsdxDiagram diagram = new VsdxDiagram(pageW, pageH);

            string title = string.IsNullOrWhiteSpace(surface.Header) ? "Threat Model" : surface.Header!;
            diagram.AddText(Math.Round(pageW / 2, 2), 0.3, 6.0, title, 0.24);

            int boundaryNum = 0;
            foreach (BorderBoundary b in surface.Borders.Values.OfType<BorderBoundary>())
            {
                boundaryNum++;
                string label = GetElementName(b);
                diagram.AddBoundary(
                    Ix(b.Left),
                    Iy(b.Top),
                    Ix(b.Left + b.Width),
                    Iy(b.Top + b.Height),
                    string.IsNullOrEmpty(label) ? "Trust boundary " + boundaryNum.ToString(System.Globalization.CultureInfo.InvariantCulture) : label);
            }

            foreach (LineBoundary lb in surface.Lines.Values.OfType<LineBoundary>())
            {
                boundaryNum++;
                string label = GetElementName(lb);
                bool straight = lb.HandleX == 0 && lb.HandleY == 0;
                int handleX = straight ? (lb.SourceX + lb.TargetX) / 2 : lb.HandleX;
                int handleY = straight ? (lb.SourceY + lb.TargetY) / 2 : lb.HandleY;
                diagram.AddBoundaryLine(
                    Ix(lb.SourceX),
                    Iy(lb.SourceY),
                    Ix(handleX),
                    Iy(handleY),
                    Ix(lb.TargetX),
                    Iy(lb.TargetY),
                    string.IsNullOrEmpty(label) ? "Trust boundary " + boundaryNum.ToString(System.Globalization.CultureInfo.InvariantCulture) : label);
            }

            HashSet<Guid> nodeGuids = new HashSet<Guid>();
            foreach (DrawingElement e in surface.Borders.Values.OfType<DrawingElement>().Where(x => !(x is BorderBoundary)))
            {
                diagram.AddNode(
                    e.Guid.ToString(),
                    Ix(e.Left + (e.Width / 2.0)),
                    Iy(e.Top + (e.Height / 2.0)),
                    Sz(e.Width),
                    Sz(e.Height),
                    GetElementName(e),
                    Kind(e),
                    NodeShapeData(e, model));
                nodeGuids.Add(e.Guid);
            }

            Dictionary<string, int> pairSeen = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Connector c in surface.Lines.Values.OfType<Connector>())
            {
                if (c.SourceGuid == Guid.Empty || c.TargetGuid == Guid.Empty
                    || !nodeGuids.Contains(c.SourceGuid) || !nodeGuids.Contains(c.TargetGuid))
                {
                    continue;
                }

                string a = c.SourceGuid.ToString();
                string b = c.TargetGuid.ToString();
                string key = string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;
                pairSeen.TryGetValue(key, out int occ);
                occ++;
                pairSeen[key] = occ;
                double bow = occ == 1 ? 0.0 : (occ % 2 == 0 ? 0.18 : -0.18);
                diagram.AddFlow(a, b, GetElementName(c), false, bow);
            }

            return diagram;
        }

        private static string Kind(DrawingElement element)
        {
            return element switch
            {
                StencilEllipse => "proc",
                StencilParallelLines => "store",
                _ => "ext",
            };
        }

        private static byte[] LoadTemplate()
        {
            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(TemplateResource)
                ?? throw new InvalidOperationException("Embedded Visio template not found: " + TemplateResource);
            using MemoryStream buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
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

        private static (Dictionary<int, int> Source, Dictionary<int, int> Target) ParseConnects(XDocument page)
        {
            Dictionary<int, int> source = new Dictionary<int, int>();
            Dictionary<int, int> target = new Dictionary<int, int>();
            foreach (XElement connect in page.Descendants().Where(e => e.Name.LocalName == "Connect"))
            {
                int from = ParseInt(connect.Attribute("FromSheet")?.Value);
                int to = ParseInt(connect.Attribute("ToSheet")?.Value);
                string fromCell = connect.Attribute("FromCell")?.Value ?? string.Empty;
                if (fromCell.IndexOf("BeginX", StringComparison.Ordinal) >= 0)
                {
                    source[from] = to;
                }
                else if (fromCell.IndexOf("EndX", StringComparison.Ordinal) >= 0)
                {
                    target[from] = to;
                }
            }

            return (source, target);
        }

        private static void BuildConnectors(
            DiagramEditor editor,
            DrawingSurfaceModel surface,
            IReadOnlyDictionary<int, int> connectorSource,
            IReadOnlyDictionary<int, int> connectorTarget,
            IReadOnlyDictionary<int, Guid> nodeGuids,
            IReadOnlyDictionary<int, string> connectorLabels)
        {
            foreach (KeyValuePair<int, int> entry in connectorSource)
            {
                if (!connectorTarget.TryGetValue(entry.Key, out int targetSheet))
                {
                    continue;
                }

                if (nodeGuids.TryGetValue(entry.Value, out Guid sourceGuid)
                    && nodeGuids.TryGetValue(targetSheet, out Guid targetGuid))
                {
                    Guid connector = editor.AddConnector(surface, sourceGuid, targetGuid);
                    editor.SetElementName(surface, connector, connectorLabels.TryGetValue(entry.Key, out string? label) ? label : string.Empty);
                }
            }
        }

        private static Dictionary<int, string> ParseMasters(string mastersXml)
        {
            Dictionary<int, string> masters = new Dictionary<int, string>();
            if (string.IsNullOrEmpty(mastersXml))
            {
                return masters;
            }

            try
            {
                XDocument document = XDocument.Parse(mastersXml);
                foreach (XElement master in document.Descendants().Where(e => e.Name.LocalName == "Master"))
                {
                    int id = ParseInt(master.Attribute("ID")?.Value);
                    string name = master.Attribute("NameU")?.Value ?? master.Attribute("Name")?.Value ?? string.Empty;
                    if (id > 0 && !string.IsNullOrEmpty(name))
                    {
                        masters[id] = name;
                    }
                }
            }
            catch (System.Xml.XmlException)
            {
                // An unparseable masters part just means classification falls back to the label text.
            }

            return masters;
        }

        private static bool IsConnector(int id, IReadOnlyDictionary<string, string> cells, string masterName, ISet<int> connectorIds)
        {
            return connectorIds.Contains(id)
                || cells.ContainsKey("BeginX")
                || masterName.IndexOf("connector", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static StencilKind? ClassifyShape(IReadOnlyDictionary<string, string> cells, string masterName, string text, bool isConnected)
        {
            // Tier 1: a package this provider wrote encodes the kind explicitly.
            if (ParseInt(cells.TryGetValue("FillPattern", out string? fillPattern) ? fillPattern : "0") == 1)
            {
                return KindFromFill(cells.TryGetValue("FillForegnd", out string? fill) ? fill : string.Empty);
            }

            if (string.IsNullOrEmpty(masterName)
                && ParseInt(cells.TryGetValue("LinePattern", out string? linePattern) ? linePattern : "0") == 2)
            {
                return StencilKind.TrustBoundary;
            }

            // Tier 3: infer the kind from the master name and label of an arbitrary diagram.
            string signal = (masterName + " " + text).ToLowerInvariant();
            if (ContainsAny(signal, BoundaryKeywords))
            {
                return StencilKind.TrustBoundary;
            }

            if (string.IsNullOrEmpty(masterName) && !isConnected)
            {
                // Most likely a floating title or annotation, not a diagram element.
                return null;
            }

            return KindFromKeywords(signal);
        }

        private static StencilKind KindFromKeywords(string signal)
        {
            if (ContainsAny(signal, DataStoreKeywords))
            {
                return StencilKind.DataStore;
            }

            if (ContainsAny(signal, ExternalKeywords))
            {
                return StencilKind.ExternalEntity;
            }

            return StencilKind.Process;
        }

        private static bool ContainsAny(string signal, IReadOnlyList<string> keywords)
        {
            foreach (string keyword in keywords)
            {
                if (signal.IndexOf(keyword, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<(string Label, string Value)> NodeShapeData(DrawingElement element, ThreatModel model)
        {
            List<(string Label, string Value)> data = new List<(string Label, string Value)>();
            foreach (KeyValuePair<string, string> property in DiagramElementHelper.GetCustomProperties(element))
            {
                data.Add((property.Key, property.Value));
            }

            List<Threat> threats = model.AllThreatsDictionary.Values
                .Where(t => t.SourceGuid == element.Guid || t.TargetGuid == element.Guid)
                .ToList();
            if (threats.Count > 0)
            {
                data.Add(("Threats", threats.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                int index = 1;
                foreach (Threat threat in threats)
                {
                    data.Add(("Threat " + index.ToString(System.Globalization.CultureInfo.InvariantCulture), ThreatSummary(threat)));
                    index++;
                }
            }

            return data;
        }

        private static string ThreatSummary(Threat threat)
        {
            string title = !string.IsNullOrEmpty(threat.Title)
                ? threat.Title!
                : !string.IsNullOrEmpty(threat.UserThreatShortDescription) ? threat.UserThreatShortDescription! : "Threat";
            List<string> parts = new List<string> { title };
            if (!string.IsNullOrEmpty(threat.UserThreatCategory))
            {
                parts.Add(threat.UserThreatCategory!);
            }

            parts.Add(threat.State.ToString());
            return string.Join(" \u00b7 ", parts);
        }

        private static void ApplyShapeData(DrawingSurfaceModel surface, Guid guid, IReadOnlyList<(string Label, string Value)> shapeData)
        {
            if (shapeData.Count == 0)
            {
                return;
            }

            Entity? element = DiagramEditor.FindElement(surface, guid);
            if (element == null)
            {
                return;
            }

            foreach ((string label, string value) in shapeData)
            {
                DiagramElementHelper.SetCustomProperty(element, label, value);
            }
        }

        private static IReadOnlyList<(string Label, string Value)> ReadShapeData(XElement shape)
        {
            XElement? section = shape.Elements().FirstOrDefault(e =>
                e.Name.LocalName == "Section" && string.Equals(e.Attribute("N")?.Value, "Property", StringComparison.Ordinal));
            if (section == null)
            {
                return Array.Empty<(string, string)>();
            }

            List<(string Label, string Value)> data = new List<(string Label, string Value)>();
            foreach (XElement row in section.Elements().Where(e => e.Name.LocalName == "Row"))
            {
                string label = RowCell(row, "Label");
                if (!string.IsNullOrEmpty(label))
                {
                    data.Add((label, RowCell(row, "Value")));
                }
            }

            return data;
        }

        private static string RowCell(XElement row, string name)
        {
            XElement? cell = row.Elements().FirstOrDefault(e =>
                e.Name.LocalName == "Cell" && string.Equals(e.Attribute("N")?.Value, name, StringComparison.Ordinal));
            return cell?.Attribute("V")?.Value ?? string.Empty;
        }

        private static string ReadEntry(ZipArchiveEntry entry)
        {
            using Stream entryStream = entry.Open();
            using StreamReader reader = new StreamReader(entryStream);
            return reader.ReadToEnd();
        }

        private static double ReadPageHeight(string pagesXml)
        {
            if (string.IsNullOrEmpty(pagesXml))
            {
                return DefaultPageHeightInches;
            }

            try
            {
                XDocument document = XDocument.Parse(pagesXml);
                XElement? cell = document.Descendants().FirstOrDefault(e =>
                    e.Name.LocalName == "Cell"
                    && string.Equals(e.Attribute("N")?.Value, "PageHeight", StringComparison.Ordinal));
                if (cell != null
                    && double.TryParse(cell.Attribute("V")?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed)
                    && parsed > 0)
                {
                    return parsed;
                }
            }
            catch (System.Xml.XmlException)
            {
                // Fall back to the default page height below.
            }

            return DefaultPageHeightInches;
        }

        private static Dictionary<string, string> TopLevelCells(XElement shape)
        {
            Dictionary<string, string> cells = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (XElement cell in shape.Elements().Where(e => e.Name.LocalName == "Cell"))
            {
                string? name = cell.Attribute("N")?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    cells[name!] = cell.Attribute("V")?.Value ?? string.Empty;
                }
            }

            return cells;
        }

        private static StencilKind KindFromFill(string? fill)
        {
            switch ((fill ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "#DAE8FC":
                    return StencilKind.Process;
                case "#FFF2CC":
                    return StencilKind.DataStore;
                case "#D5E8D4":
                    return StencilKind.ExternalEntity;
                default:
                    return StencilKind.ExternalEntity;
            }
        }

        private static (int Left, int Top, int Width, int Height) NodeBounds(IReadOnlyDictionary<string, string> cells, double pageHeight)
        {
            double pinX = ParseDouble(cells, "PinX");
            double pinY = ParseDouble(cells, "PinY");
            double width = ParseDouble(cells, "Width");
            double height = ParseDouble(cells, "Height");
            double leftInches = pinX - (width / 2.0);
            double topInches = pageHeight - pinY - (height / 2.0);
            return (
                (int)Math.Round(leftInches * PixelsPerInch),
                (int)Math.Round(topInches * PixelsPerInch),
                (int)Math.Round(width * PixelsPerInch),
                (int)Math.Round(height * PixelsPerInch));
        }

        private static double ParseDouble(IReadOnlyDictionary<string, string> cells, string key)
        {
            return cells.TryGetValue(key, out string? value)
                && double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double result)
                ? result
                : 0.0;
        }

        private static int ParseInt(string? value)
        {
            return int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int result)
                ? result
                : 0;
        }
    }
}
