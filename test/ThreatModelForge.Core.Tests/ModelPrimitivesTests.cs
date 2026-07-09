namespace ThreatModelForge.Core.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the primitive behaviors of the Core model types: lazily initialized collections,
    /// the case-insensitive threat instance dictionary, and the computed getters on the line-element
    /// base type.
    /// </summary>
    [TestClass]
    public class ModelPrimitivesTests
    {
        /// <summary>
        /// A freshly constructed model exposes non-null, empty collections rather than null.
        /// </summary>
        [TestMethod]
        public void NewModelExposesNonNullEmptyCollections()
        {
            ThreatModel model = new ThreatModel();

            Assert.IsNotNull(model.DrawingSurfaceList);
            Assert.AreEqual(0, model.DrawingSurfaceList.Count);
            Assert.IsNotNull(model.Notes);
            Assert.AreEqual(0, model.Notes.Count);
            Assert.IsNotNull(model.Validations);
            Assert.AreEqual(0, model.Validations.Count);
            Assert.IsNotNull(model.AllThreatsDictionary);
            Assert.AreEqual(0, model.AllThreatsDictionary.Count);
        }

        /// <summary>
        /// The threat instance dictionary is keyed case-insensitively, so a threat stored under one
        /// casing is retrievable under another.
        /// </summary>
        [TestMethod]
        public void AllThreatsDictionaryIsCaseInsensitive()
        {
            ThreatModel model = new ThreatModel();
            Threat threat = new Threat { Id = 7 };

            model.AllThreatsDictionary["Flow.KEY"] = threat;

            Assert.IsTrue(model.AllThreatsDictionary.ContainsKey("flow.key"));
            Assert.AreSame(threat, model.AllThreatsDictionary["FLOW.key"]);
        }

        /// <summary>
        /// A drawing surface exposes non-null, empty border and line collections rather than null.
        /// </summary>
        [TestMethod]
        public void DrawingSurfaceExposesNonNullBordersAndLines()
        {
            DrawingSurfaceModel surface = new DrawingSurfaceModel();

            Assert.IsNotNull(surface.Borders);
            Assert.AreEqual(0, surface.Borders.Count);
            Assert.IsNotNull(surface.Lines);
            Assert.AreEqual(0, surface.Lines.Count);
        }

        /// <summary>
        /// An entity's display-property list is lazily created, non-null, and accepts additions.
        /// </summary>
        [TestMethod]
        public void EntityPropertiesAreLazilyInitialized()
        {
            StencilEllipse entity = new StencilEllipse();

            Assert.IsNotNull(entity.Properties);
            Assert.AreEqual(0, entity.Properties.Count);

            entity.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = "Web" });
            Assert.AreEqual(1, entity.Properties.Count);
        }

        /// <summary>
        /// When the curve handle is unset, it reports the midpoint of the source and target endpoints so
        /// the line satisfies the tool's coordinate validation.
        /// </summary>
        [TestMethod]
        public void LineHandleDefaultsToMidpointOfEndpoints()
        {
            Connector connector = new Connector
            {
                SourceX = 10,
                SourceY = 40,
                TargetX = 30,
                TargetY = 80,
            };

            Assert.AreEqual(20, connector.HandleX);
            Assert.AreEqual(60, connector.HandleY);
        }

        /// <summary>
        /// An explicitly set curve handle overrides the computed midpoint.
        /// </summary>
        [TestMethod]
        public void LineHandleUsesExplicitValueWhenSet()
        {
            Connector connector = new Connector
            {
                SourceX = 10,
                TargetX = 30,
                HandleX = 7,
                HandleY = 9,
            };

            Assert.AreEqual(7, connector.HandleX);
            Assert.AreEqual(9, connector.HandleY);
        }

        /// <summary>
        /// The connection ports serialize as "None" when unset, matching the non-nullable enum member
        /// the tool expects.
        /// </summary>
        [TestMethod]
        public void LinePortsDefaultToNone()
        {
            Connector connector = new Connector();

            Assert.AreEqual("None", connector.PortSource);
            Assert.AreEqual("None", connector.PortTarget);
        }

        /// <summary>
        /// An explicitly set connection port is reported verbatim.
        /// </summary>
        [TestMethod]
        public void LinePortsUseExplicitValueWhenSet()
        {
            Connector connector = new Connector { PortSource = "Right", PortTarget = "Left" };

            Assert.AreEqual("Right", connector.PortSource);
            Assert.AreEqual("Left", connector.PortTarget);
        }
    }
}
