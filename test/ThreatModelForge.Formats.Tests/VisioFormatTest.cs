namespace ThreatModelForge.Formats.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for <see cref="VisioFormat"/>.
    /// </summary>
    [TestClass]
    public class VisioFormatTest
    {
        /// <summary>
        /// Gets or sets the test context, used to locate deployed fixtures.
        /// </summary>
        public TestContext? TestContext { get; set; }

        /// <summary>
        /// Verifies that a real <c>.tm7</c> model writes to a structurally valid <c>.vsdx</c>
        /// package (well-formed parts, required OPC parts present) whose injected page carries
        /// shapes and connectors.
        /// </summary>
        [TestMethod]
        public void WritesValidVsdxFromTm7()
        {
            string sourcePath = Path.Combine(this.TestContext!.DeploymentDirectory!, "SampleModel.tm7");
            ThreatModel model = ThreatModelFormatRegistry.CreateDefault().Load(sourcePath);

            byte[] vsdx;
            using (MemoryStream buffer = new MemoryStream())
            {
                new VisioFormat().Write(model, buffer);
                vsdx = buffer.ToArray();
            }

            using MemoryStream zipStream = new MemoryStream(vsdx, writable: false);
            using ZipArchive zip = new ZipArchive(zipStream, ZipArchiveMode.Read);

            Assert.IsNotNull(zip.GetEntry("[Content_Types].xml"));
            Assert.IsNotNull(zip.GetEntry("visio/document.xml"));
            ZipArchiveEntry? page = zip.GetEntry("visio/pages/page1.xml");
            Assert.IsNotNull(page);

            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                if (entry.FullName.EndsWith(".xml", StringComparison.Ordinal)
                    || entry.FullName.EndsWith(".rels", StringComparison.Ordinal))
                {
                    using Stream partStream = entry.Open();
                    XDocument.Load(partStream);
                }
            }

            using Stream pageStream = page!.Open();
            XDocument pageDoc = XDocument.Load(pageStream);
            Assert.IsTrue(pageDoc.Descendants().Any(e => e.Name.LocalName == "Shape"), "expected shapes");
            Assert.IsTrue(pageDoc.Descendants().Any(e => e.Name.LocalName == "Connect"), "expected connectors");
        }

        /// <summary>
        /// Verifies that the provider advertises read and write capabilities.
        /// </summary>
        [TestMethod]
        public void AdvertisesReadWrite()
        {
            VisioFormat format = new VisioFormat();

            Assert.IsTrue(format.Capabilities.CanRead);
            Assert.IsTrue(format.Capabilities.CanWrite);
        }

        /// <summary>
        /// Verifies content sniffing matches a Visio package (an OPC zip with a
        /// <c>visio/document.xml</c> part) and rejects non-Visio content, leaving the stream
        /// position unchanged.
        /// </summary>
        [TestMethod]
        public void CanReadSniffsVsdxPackage()
        {
            string sourcePath = Path.Combine(this.TestContext!.DeploymentDirectory!, "SampleModel.tm7");
            ThreatModel model = ThreatModelFormatRegistry.CreateDefault().Load(sourcePath);

            VisioFormat format = new VisioFormat();
            using (MemoryStream vsdx = new MemoryStream())
            {
                format.Write(model, vsdx);
                vsdx.Position = 0;
                Assert.IsTrue(format.CanRead(vsdx));
                Assert.AreEqual(0, vsdx.Position);
            }

            using (MemoryStream notZip = new MemoryStream(Encoding.UTF8.GetBytes("<ThreatModel/>")))
            {
                Assert.IsFalse(format.CanRead(notZip));
            }
        }

        /// <summary>
        /// Verifies that the structural diagram survives a write-then-read round-trip through the
        /// engine model: the node count and the connector count are preserved when a real
        /// <c>.tm7</c> model is exported to <c>.vsdx</c> and imported back.
        /// </summary>
        [TestMethod]
        public void RoundTripsStructureThroughModel()
        {
            string sourcePath = Path.Combine(this.TestContext!.DeploymentDirectory!, "SampleModel.tm7");
            ThreatModel original = ThreatModelFormatRegistry.CreateDefault().Load(sourcePath);

            VisioFormat format = new VisioFormat();
            ThreatModel roundTripped;
            using (MemoryStream vsdx = new MemoryStream())
            {
                format.Write(original, vsdx);
                vsdx.Position = 0;
                roundTripped = format.Read(vsdx);
            }

            int originalNodes = CountNodes(original);
            int originalConnectors = CountConnectors(original);

            Assert.IsTrue(originalNodes > 0, "fixture should contain at least one component");
            Assert.IsTrue(originalConnectors > 0, "fixture should contain at least one connector");
            Assert.AreEqual(originalNodes, CountNodes(roundTripped), "node count should survive the round-trip");
            Assert.AreEqual(originalConnectors, CountConnectors(roundTripped), "connector count should survive the round-trip");
        }

        /// <summary>
        /// Verifies Tier-3 heuristic import: an arbitrary Visio diagram (standard stencil masters,
        /// none of this provider's own fill/pattern markers) is classified by master name and label
        /// into processes, data stores, external entities, and trust boundaries, with flows rebuilt
        /// from the <c>Connects</c> glue.
        /// </summary>
        [TestMethod]
        public void ImportsArbitraryVisioByHeuristics()
        {
            VisioFormat format = new VisioFormat();
            ThreatModel model;
            using (MemoryStream vsdx = new MemoryStream(BuildArbitraryVisio(), writable: false))
            {
                Assert.IsTrue(format.CanRead(vsdx));
                vsdx.Position = 0;
                model = format.Read(vsdx);
            }

            DrawingSurfaceModel surface = model.DrawingSurfaceList.Single();
            Assert.AreEqual(1, surface.Borders.Values.OfType<StencilEllipse>().Count(), "a 'Server' master maps to a process");
            Assert.AreEqual(1, surface.Borders.Values.OfType<StencilParallelLines>().Count(), "a 'Database' master maps to a data store");
            Assert.AreEqual(1, surface.Borders.Values.OfType<StencilRectangle>().Count(), "a 'User' master maps to an external entity");
            Assert.AreEqual(1, surface.Borders.Values.OfType<BorderBoundary>().Count(), "a 'Trust Boundary' master maps to a boundary");
            Assert.AreEqual(1, surface.Lines.Values.OfType<Connector>().Count(), "the dynamic connector becomes one flow");
        }

        /// <summary>
        /// Verifies that a trust-boundary line with an offset handle is exported as a dashed,
        /// multi-segment curve (a tessellated Bezier), not a bounding box.
        /// </summary>
        [TestMethod]
        public void WritesCurvedBoundaryLineGeometry()
        {
            DrawingSurfaceModel surface = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "D" };
            LineBoundary boundary = new LineBoundary
            {
                Guid = Guid.NewGuid(),
                SourceX = 0,
                SourceY = 0,
                TargetX = 400,
                TargetY = 0,
                HandleX = 200,
                HandleY = 160,
            };
            surface.Lines[boundary.Guid] = boundary;
            ThreatModel model = new ThreatModel();
            model.DrawingSurfaceList.Add(surface);

            byte[] vsdx;
            using (MemoryStream buffer = new MemoryStream())
            {
                new VisioFormat().Write(model, buffer);
                vsdx = buffer.ToArray();
            }

            using MemoryStream zipStream = new MemoryStream(vsdx, writable: false);
            using ZipArchive zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
            ZipArchiveEntry? pageEntry = zip.GetEntry("visio/pages/page1.xml");
            Assert.IsNotNull(pageEntry);
            using Stream pageStream = pageEntry!.Open();
            XDocument page = XDocument.Load(pageStream);

            bool hasCurve = page.Descendants().Any(shape =>
                shape.Name.LocalName == "Shape"
                && shape.Elements().Any(c => c.Name.LocalName == "Cell" && (string?)c.Attribute("N") == "LinePattern" && (string?)c.Attribute("V") == "2")
                && shape.Descendants().Count(r => r.Name.LocalName == "Row" && (string?)r.Attribute("T") == "LineTo") >= 10);
            Assert.IsTrue(hasCurve, "expected a dashed, multi-segment boundary curve shape");
        }

        /// <summary>
        /// Verifies that a node's custom properties and the threats associated with it are written as
        /// per-shape Visio Shape Data (a <c>Property</c> section with Label/Value rows).
        /// </summary>
        [TestMethod]
        public void WritesThreatsAndPropertiesAsShapeData()
        {
            StencilEllipse process = new StencilEllipse { Guid = Guid.NewGuid(), TypeId = "GE.P", GenericTypeId = "GE.P", Left = 20, Top = 20, Width = 90, Height = 50 };
            process.Properties.Add(new StringDisplayAttribute { Name = "Name", DisplayName = "Name", Value = "Web App" });
            process.Properties.Add(new CustomStringDisplayAttribute { Value = "Protocol:HTTPS" });
            StencilParallelLines store = new StencilParallelLines { Guid = Guid.NewGuid(), TypeId = "GE.DS", GenericTypeId = "GE.DS", Left = 260, Top = 20, Width = 110, Height = 50 };
            store.Properties.Add(new StringDisplayAttribute { Name = "Name", DisplayName = "Name", Value = "Database" });

            DrawingSurfaceModel surface = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "D" };
            surface.Borders[process.Guid] = process;
            surface.Borders[store.Guid] = store;

            ThreatModel model = new ThreatModel();
            model.DrawingSurfaceList.Add(surface);
            model.AllThreatsDictionary["t1"] = new Threat
            {
                SourceGuid = process.Guid,
                TargetGuid = store.Guid,
                Title = "Spoofing the Web App",
                UserThreatCategory = "Spoofing",
                State = ThreatState.NeedsInvestigation,
            };

            byte[] vsdx;
            using (MemoryStream buffer = new MemoryStream())
            {
                new VisioFormat().Write(model, buffer);
                vsdx = buffer.ToArray();
            }

            using MemoryStream zipStream = new MemoryStream(vsdx, writable: false);
            using ZipArchive zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
            ZipArchiveEntry? pageEntry = zip.GetEntry("visio/pages/page1.xml");
            Assert.IsNotNull(pageEntry);
            using Stream pageStream = pageEntry!.Open();
            XDocument page = XDocument.Load(pageStream);

            List<XElement> rows = page.Descendants()
                .Where(e => e.Name.LocalName == "Section" && (string?)e.Attribute("N") == "Property")
                .Descendants().Where(e => e.Name.LocalName == "Row")
                .ToList();

            Assert.IsTrue(rows.Any(r => RowCellValue(r, "Label") == "Threats"), "expected a Threats count property");
            Assert.IsTrue(rows.Any(r => RowCellValue(r, "Value").Contains("Spoofing the Web App")), "expected a per-threat property");
            Assert.IsTrue(rows.Any(r => RowCellValue(r, "Label") == "Protocol" && RowCellValue(r, "Value") == "HTTPS"), "expected the custom property as Shape Data");
        }

        /// <summary>
        /// Verifies that per-shape Shape Data survives a write-then-read round-trip as an element
        /// custom property.
        /// </summary>
        [TestMethod]
        public void RoundTripsShapeDataAsCustomProperties()
        {
            StencilEllipse process = new StencilEllipse { Guid = Guid.NewGuid(), TypeId = "GE.P", GenericTypeId = "GE.P", Left = 20, Top = 20, Width = 90, Height = 50 };
            process.Properties.Add(new StringDisplayAttribute { Name = "Name", DisplayName = "Name", Value = "Web App" });
            process.Properties.Add(new CustomStringDisplayAttribute { Value = "Protocol:HTTPS" });
            DrawingSurfaceModel surface = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "D" };
            surface.Borders[process.Guid] = process;
            ThreatModel original = new ThreatModel();
            original.DrawingSurfaceList.Add(surface);

            VisioFormat format = new VisioFormat();
            ThreatModel roundTripped;
            using (MemoryStream buffer = new MemoryStream())
            {
                format.Write(original, buffer);
                buffer.Position = 0;
                roundTripped = format.Read(buffer);
            }

            DrawingElement node = roundTripped.DrawingSurfaceList
                .SelectMany(s => s.Borders.Values.OfType<DrawingElement>())
                .First(e => !(e is BorderBoundary));
            bool hasProtocol = node.Properties.OfType<CustomStringDisplayAttribute>()
                .Any(p => string.Equals(p.Value as string, "Protocol:HTTPS", StringComparison.Ordinal));
            Assert.IsTrue(hasProtocol, "the custom property should survive the Shape Data round-trip");
        }

        private static string RowCellValue(XElement row, string name)
        {
            XElement? cell = row.Elements().FirstOrDefault(c => c.Name.LocalName == "Cell" && (string?)c.Attribute("N") == name);
            return (string?)cell?.Attribute("V") ?? string.Empty;
        }

        private static int CountNodes(ThreatModel model)
        {
            return model.DrawingSurfaceList.Sum(surface =>
                surface.Borders.Values.OfType<DrawingElement>().Count(e => !(e is BorderBoundary)));
        }

        private static int CountConnectors(ThreatModel model)
        {
            int total = 0;
            foreach (DrawingSurfaceModel surface in model.DrawingSurfaceList)
            {
                HashSet<Guid> nodeIds = new HashSet<Guid>(
                    surface.Borders.Values.OfType<DrawingElement>().Where(e => !(e is BorderBoundary)).Select(e => e.Guid));
                total += surface.Lines.Values.OfType<Connector>()
                    .Count(c => nodeIds.Contains(c.SourceGuid) && nodeIds.Contains(c.TargetGuid));
            }

            return total;
        }

        private static byte[] BuildArbitraryVisio()
        {
            Dictionary<string, string> parts = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["visio/document.xml"] = "<VisioDocument xmlns='http://schemas.microsoft.com/office/visio/2012/main'/>",
                ["visio/pages/pages.xml"] =
                    "<Pages xmlns='http://schemas.microsoft.com/office/visio/2012/main'>"
                    + "<Page ID='0' Name='Page-1'><PageSheet><Cell N='PageWidth' V='11'/>"
                    + "<Cell N='PageHeight' V='8.5'/></PageSheet></Page></Pages>",
                ["visio/masters/masters.xml"] =
                    "<Masters xmlns='http://schemas.microsoft.com/office/visio/2012/main'>"
                    + "<Master ID='2' NameU='Server'/><Master ID='3' NameU='Database'/>"
                    + "<Master ID='4' NameU='User'/><Master ID='5' NameU='Dynamic connector'/>"
                    + "<Master ID='6' NameU='Trust Boundary'/></Masters>",
                ["visio/pages/page1.xml"] =
                    "<PageContents xmlns='http://schemas.microsoft.com/office/visio/2012/main'><Shapes>"
                    + NodeShape(1, 2, 2.0, 6.0, "Web Server")
                    + NodeShape(2, 3, 6.0, 6.0, "Orders Database")
                    + NodeShape(3, 4, 2.0, 2.0, "Customer")
                    + BoundaryShape(5, 6, 4.0, 4.0, "DMZ")
                    + ConnectorShape(4, 5)
                    + "</Shapes><Connects>"
                    + "<Connect FromSheet='4' FromCell='BeginX' ToSheet='1'/>"
                    + "<Connect FromSheet='4' FromCell='EndX' ToSheet='2'/>"
                    + "</Connects></PageContents>",
            };

            using MemoryStream buffer = new MemoryStream();
            using (ZipArchive zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (KeyValuePair<string, string> part in parts)
                {
                    ZipArchiveEntry entry = zip.CreateEntry(part.Key);
                    using StreamWriter writer = new StreamWriter(entry.Open());
                    writer.Write(part.Value);
                }
            }

            return buffer.ToArray();
        }

        private static string NodeShape(int id, int master, double pinX, double pinY, string label)
        {
            return "<Shape ID='" + id.ToString(CultureInfo.InvariantCulture) + "' Type='Shape' Master='" + master.ToString(CultureInfo.InvariantCulture) + "'>"
                + "<Cell N='PinX' V='" + pinX.ToString(CultureInfo.InvariantCulture) + "'/>"
                + "<Cell N='PinY' V='" + pinY.ToString(CultureInfo.InvariantCulture) + "'/>"
                + "<Cell N='Width' V='1.5'/><Cell N='Height' V='0.75'/>"
                + "<Text>" + label + "</Text></Shape>";
        }

        private static string BoundaryShape(int id, int master, double pinX, double pinY, string label)
        {
            return "<Shape ID='" + id.ToString(CultureInfo.InvariantCulture) + "' Type='Shape' Master='" + master.ToString(CultureInfo.InvariantCulture) + "'>"
                + "<Cell N='PinX' V='" + pinX.ToString(CultureInfo.InvariantCulture) + "'/>"
                + "<Cell N='PinY' V='" + pinY.ToString(CultureInfo.InvariantCulture) + "'/>"
                + "<Cell N='Width' V='3'/><Cell N='Height' V='3'/>"
                + "<Text>" + label + "</Text></Shape>";
        }

        private static string ConnectorShape(int id, int master)
        {
            return "<Shape ID='" + id.ToString(CultureInfo.InvariantCulture) + "' Type='Shape' Master='" + master.ToString(CultureInfo.InvariantCulture) + "'>"
                + "<Cell N='BeginX' V='2'/><Cell N='BeginY' V='6'/><Cell N='EndX' V='6'/><Cell N='EndY' V='6'/>"
                + "<Text>reads</Text></Shape>";
        }
    }
}
