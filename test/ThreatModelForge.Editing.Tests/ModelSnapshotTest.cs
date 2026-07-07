namespace ThreatModelForge.Editing.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Editing;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for <see cref="ModelSnapshot"/>.
    /// </summary>
    [TestClass]
    public class ModelSnapshotTest
    {
        /// <summary>
        /// Verifies that every element on every surface is captured.
        /// </summary>
        [TestMethod]
        public void CaptureIncludesEveryElement()
        {
            ThreatModel model = Build();

            IReadOnlyList<ElementDescriptor> descriptors = ModelSnapshot.Capture(model);

            Assert.AreEqual(3, descriptors.Count);
        }

        /// <summary>
        /// Verifies that elements are classified and named, and carry their diagram name.
        /// </summary>
        [TestMethod]
        public void CaptureClassifiesAndNamesElements()
        {
            ThreatModel model = Build();

            IReadOnlyList<ElementDescriptor> descriptors = ModelSnapshot.Capture(model);

            ElementDescriptor process = descriptors.Single(descriptor => descriptor.Kind == "process");
            Assert.AreEqual("Web App", process.Name);
            Assert.AreEqual("Main", process.DiagramName);
        }

        /// <summary>
        /// Verifies that a flow's endpoints and an element's custom properties appear as attributes.
        /// </summary>
        [TestMethod]
        public void CaptureRecordsEndpointsAndCustomProperties()
        {
            ThreatModel model = Build();

            ElementDescriptor flow = ModelSnapshot.Capture(model).Single(descriptor => descriptor.Kind == "flow");

            Assert.IsTrue(flow.Attributes.ContainsKey(ModelSnapshot.SourceKey));
            Assert.IsTrue(flow.Attributes.ContainsKey(ModelSnapshot.TargetKey));
            Assert.AreEqual("HTTP", flow.Attributes["Protocol"]);
        }

        private static ThreatModel Build()
        {
            Guid process = Guid.NewGuid();
            Guid store = Guid.NewGuid();
            Guid flow = Guid.NewGuid();

            StencilRectangle processElement = new StencilRectangle { Guid = process, TypeId = "GE.P" };
            DiagramElementHelper.SetName(processElement, "Web App");

            StencilEllipse storeElement = new StencilEllipse { Guid = store, TypeId = "GE.DS" };
            DiagramElementHelper.SetName(storeElement, "Database");

            Connector flowElement = new Connector { Guid = flow, TypeId = "GE.DF", SourceGuid = process, TargetGuid = store };
            DiagramElementHelper.SetName(flowElement, "query");
            DiagramElementHelper.SetCustomProperty(flowElement, "Protocol", "HTTP");

            DrawingSurfaceModel surface = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "Main" };
            surface.Borders[process] = processElement;
            surface.Borders[store] = storeElement;
            surface.Lines[flow] = flowElement;

            ThreatModel model = new ThreatModel();
            model.DrawingSurfaceList.Add(surface);
            return model;
        }
    }
}
