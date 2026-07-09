namespace ThreatModelForge.Core.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Regression tests that assert the <em>serialized</em> <c>.tm7</c> bytes a freshly authored model
    /// produces carry the document scaffolding the Microsoft Threat Modeling Tool (MTMT) reads without
    /// a null guard when it opens a file. Unlike <see cref="ThreatModelScaffoldingTests"/>, which
    /// inspect the in-memory model after <see cref="ThreatModel.Save(System.IO.Stream)"/>, these parse
    /// the on-disk XML so a serialization regression (a missing member, an <c>i:nil</c> where MTMT
    /// expects a value, a non-nullable enum written as nil) is caught even if the model object looks
    /// correct — the class of defect that stops a generated model opening in MTMT.
    /// </summary>
    [TestClass]
    public class Tm7MtmtCompatibilityTests
    {
        private const string XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";

        /// <summary>
        /// MTMT refuses to open a document whose format version it does not recognize, so a generated
        /// file must carry the recognized version stamp.
        /// </summary>
        [TestMethod]
        public void SerializedModelDeclaresRecognizedDocumentVersion()
        {
            XDocument doc = SerializeAndParse(BuildAuthoredModel());

            XElement? version = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Version");
            Assert.IsNotNull(version, "The serialized model must carry a <Version> element.");
            Assert.AreEqual("4.3", version!.Value);
        }

        /// <summary>
        /// The document-level members MTMT dereferences without a null check (the metadata block and the
        /// profile's prompted knowledge-base versions) must be present in the serialized file.
        /// </summary>
        [TestMethod]
        public void SerializedModelIncludesToolScaffolding()
        {
            XDocument doc = SerializeAndParse(BuildAuthoredModel());

            XElement? meta = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "MetaInformation");
            Assert.IsNotNull(meta, "The serialized model must carry a <MetaInformation> block.");
            Assert.IsNull(NilAttribute(meta!), "<MetaInformation> must not be serialized as nil.");

            Assert.IsTrue(
                doc.Descendants().Any(e => e.Name.LocalName == "PromptedKb"),
                "The serialized model must carry the profile's <PromptedKb> versions block.");
        }

        /// <summary>
        /// Every drawing surface must declare the drawing-surface type identifiers and mirror its title
        /// into the "Name" display property MTMT binds the page name to.
        /// </summary>
        [TestMethod]
        public void SerializedSurfaceCarriesDrawingSurfaceTypeAndName()
        {
            XDocument doc = SerializeAndParse(BuildAuthoredModel());

            XElement surface = doc.Descendants().First(e => e.Name.LocalName == "DrawingSurfaceModel");

            Assert.AreEqual(
                "DRAWINGSURFACE",
                surface.Elements().First(e => e.Name.LocalName == "GenericTypeId").Value);
            Assert.AreEqual(
                "DRAWINGSURFACE",
                surface.Elements().First(e => e.Name.LocalName == "TypeId").Value);

            // The surface's own Properties block (a direct child, distinct from Borders/Lines) must
            // include the mirrored "Name" display property.
            XElement properties = surface.Elements().First(e => e.Name.LocalName == "Properties");
            bool hasNameProperty = properties.Elements().Any(property =>
                property.Elements().Any(child => child.Name.LocalName == "DisplayName" && child.Value == "Name"));
            Assert.IsTrue(hasNameProperty, "The surface must carry a \"Name\" display property.");
        }

        /// <summary>
        /// MTMT types a connector's ports as a non-nullable enum, so unset ports must serialize as the
        /// literal "None" rather than <c>i:nil</c> — otherwise the file fails to deserialize.
        /// </summary>
        [TestMethod]
        public void SerializedConnectorPortsAreNoneNotNil()
        {
            XDocument doc = SerializeAndParse(BuildAuthoredModel());

            var ports = doc.Descendants()
                .Where(e => e.Name.LocalName == "PortSource" || e.Name.LocalName == "PortTarget")
                .ToList();

            Assert.IsTrue(ports.Count >= 2, "Expected the connector to serialize both ports.");
            Assert.IsTrue(
                ports.All(p => NilAttribute(p) == null && p.Value == "None"),
                "Connector ports must serialize as \"None\", never nil.");
        }

        /// <summary>
        /// The generated file loads back through the same engine without loss, proving the serialized
        /// document is self-consistent (a corrupt document that MTMT would reject typically fails to
        /// re-open here too).
        /// </summary>
        [TestMethod]
        public void GeneratedModelReloadsWithoutLoss()
        {
            byte[] bytes = Serialize(BuildAuthoredModel());

            ThreatModel reloaded;
            using (MemoryStream stream = new MemoryStream(bytes))
            {
                reloaded = ThreatModel.Load(stream);
            }

            Assert.AreEqual(1, reloaded.DrawingSurfaceList.Count);
            DrawingSurfaceModel surface = reloaded.DrawingSurfaceList[0];
            Assert.AreEqual(2, surface.Borders.Count);
            Assert.AreEqual(1, surface.Lines.Count);
            Assert.AreEqual("4.3", reloaded.Version);
        }

        private static XAttribute? NilAttribute(XElement element)
        {
            return element.Attribute(XName.Get("nil", XsiNamespace));
        }

        private static byte[] Serialize(ThreatModel model)
        {
            using MemoryStream stream = new MemoryStream();
            model.Save(stream);
            return stream.ToArray();
        }

        private static XDocument SerializeAndParse(ThreatModel model)
        {
            using MemoryStream stream = new MemoryStream(Serialize(model));
            return XDocument.Load(stream);
        }

        /// <summary>
        /// Builds a small but representative diagram — a process and a data store connected by a data
        /// flow, inside one surface — so the serialized document exercises the surface, element, and
        /// connector scaffolding a real generated model would carry.
        /// </summary>
        private static ThreatModel BuildAuthoredModel()
        {
            StencilEllipse process = Boxed<StencilEllipse>("GE.P", "Web App");
            StencilParallelLines store = Boxed<StencilParallelLines>("GE.DS", "Database");

            Connector flow = new Connector
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = "GE.DF",
                SourceGuid = process.Guid,
                TargetGuid = store.Guid,
                SourceX = 100,
                SourceY = 100,
                TargetX = 300,
                TargetY = 100,
            };

            DrawingSurfaceModel surface = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "Context" };
            surface.Borders.Add(process.Guid, process);
            surface.Borders.Add(store.Guid, store);
            surface.Lines.Add(flow.Guid, flow);

            ThreatModel model = new ThreatModel();
            model.DrawingSurfaceList.Add(surface);
            return model;
        }

        private static TElement Boxed<TElement>(string genericTypeId, string name)
            where TElement : DrawingElement, new()
        {
            TElement element = new TElement
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = genericTypeId,
            };
            element.Properties.Add(new HeaderDisplayAttribute { DisplayName = name, Name = name });
            element.Properties.Add(new StringDisplayAttribute { Name = "Name", DisplayName = "Name", Value = name });
            return element;
        }
    }
}
