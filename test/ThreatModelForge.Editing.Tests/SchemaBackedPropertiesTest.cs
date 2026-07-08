namespace ThreatModelForge.Editing.Tests
{
    using System;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Editing;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for <see cref="SchemaBackedProperties"/>.
    /// </summary>
    [TestClass]
    public class SchemaBackedPropertiesTest
    {
        /// <summary>
        /// Verifies that a null model is rejected.
        /// </summary>
        [TestMethod]
        public void ApplyThrowsForNullModel()
        {
            Assert.Throws<ArgumentNullException>(
                () => SchemaBackedProperties.Apply(null!, new KnowledgeBaseData()));
        }

        /// <summary>
        /// Verifies that a null knowledge base is rejected.
        /// </summary>
        [TestMethod]
        public void ApplyThrowsForNullKnowledgeBase()
        {
            Assert.Throws<ArgumentNullException>(
                () => SchemaBackedProperties.Apply(new ThreatModel(), null!));
        }

        /// <summary>
        /// Verifies that the schema's enumerated properties are declared as list attributes on the
        /// matching generic element type, while free-text properties are not.
        /// </summary>
        [TestMethod]
        public void ApplyDeclaresListAttributesOnGenericElementType()
        {
            KnowledgeBaseData knowledgeBase = BuildKnowledgeBase("GE.DF");

            SchemaBackedProperties.Apply(new ThreatModel(), knowledgeBase);

            ElementType flow = knowledgeBase.GenericElements.Single();
            KnowledgeBaseAttribute? protocol = flow.Attributes.FirstOrDefault(a => a.DisplayName == "Protocol");
            Assert.IsNotNull(protocol);
            Assert.AreEqual(AttributeType.List, protocol!.Type);
            Assert.AreEqual(AttributeMode.Dynamic, protocol.Mode);
            Assert.AreEqual("Select", protocol.AttributeValues.First());
            CollectionAssert.Contains(protocol.AttributeValues, "HTTPS");

            // Free-text properties such as Port cannot be expressed as a list and are not declared.
            Assert.IsFalse(flow.Attributes.Any(a => a.DisplayName == "Port"));
        }

        /// <summary>
        /// Verifies that an enumerated custom property is rewritten as a typed list selection whose
        /// selected index points at the stored value.
        /// </summary>
        [TestMethod]
        public void ApplyTypesEnumPropertyAsListSelection()
        {
            (ThreatModel model, Connector flow) = ModelWithFlow();
            DiagramElementHelper.SetCustomProperty(flow, "Protocol", "HTTPS");

            SchemaBackedProperties.Apply(model, BuildKnowledgeBase("GE.DF"));

            Assert.IsFalse(flow.Properties.OfType<CustomStringDisplayAttribute>()
                .Any(p => (p.Value as string ?? string.Empty).StartsWith("Protocol:", StringComparison.Ordinal)));
            ListDisplayAttribute typed = flow.Properties.OfType<ListDisplayAttribute>()
                .Single(p => p.DisplayName == "Protocol");
            string[] options = (string[])typed.Value!;
            Assert.AreEqual("Select", options[0]);
            Assert.AreEqual("HTTPS", options[typed.SelectedIndex]);
        }

        /// <summary>
        /// Verifies that a boolean custom property becomes a Select/Yes/No list selection.
        /// </summary>
        [TestMethod]
        public void ApplyTypesBooleanPropertyAsYesNoList()
        {
            (ThreatModel model, StencilParallelLines store) = ModelWithStore();
            DiagramElementHelper.SetCustomProperty(store, "StoresCredentials", "Yes");

            SchemaBackedProperties.Apply(model, BuildKnowledgeBase("GE.DS"));

            ListDisplayAttribute typed = store.Properties.OfType<ListDisplayAttribute>()
                .Single(p => p.DisplayName == "StoresCredentials");
            string[] options = (string[])typed.Value!;
            CollectionAssert.AreEqual(new[] { "Select", "Yes", "No" }, options);
            Assert.AreEqual("Yes", options[typed.SelectedIndex]);
        }

        /// <summary>
        /// Verifies that a free-text schema property is left as a custom attribute.
        /// </summary>
        [TestMethod]
        public void ApplyLeavesFreeTextPropertyAsCustom()
        {
            (ThreatModel model, Connector flow) = ModelWithFlow();
            DiagramElementHelper.SetCustomProperty(flow, "Port", "443");

            SchemaBackedProperties.Apply(model, BuildKnowledgeBase("GE.DF"));

            Assert.IsFalse(flow.Properties.OfType<ListDisplayAttribute>().Any());
            Assert.AreEqual("443", DiagramElementHelper.GetCustomProperties(flow)["Port"]);
        }

        /// <summary>
        /// Verifies that a property outside the schema is left as a custom attribute.
        /// </summary>
        [TestMethod]
        public void ApplyLeavesUnknownPropertyAsCustom()
        {
            (ThreatModel model, Connector flow) = ModelWithFlow();
            DiagramElementHelper.SetCustomProperty(flow, "Alias", "browser");

            SchemaBackedProperties.Apply(model, BuildKnowledgeBase("GE.DF"));

            Assert.IsFalse(flow.Properties.OfType<ListDisplayAttribute>().Any());
            Assert.AreEqual("browser", DiagramElementHelper.GetCustomProperties(flow)["Alias"]);
        }

        /// <summary>
        /// Verifies that a value outside the schema's options is preserved as a custom attribute rather
        /// than typed to an unset selection (which would drop the value).
        /// </summary>
        [TestMethod]
        public void ApplyPreservesUnknownValueAsCustom()
        {
            (ThreatModel model, Connector flow) = ModelWithFlow();
            DiagramElementHelper.SetCustomProperty(flow, "Protocol", "Carrier Pigeon");

            SchemaBackedProperties.Apply(model, BuildKnowledgeBase("GE.DF"));

            Assert.IsFalse(flow.Properties.OfType<ListDisplayAttribute>().Any());
            Assert.AreEqual("Carrier Pigeon", DiagramElementHelper.GetCustomProperties(flow)["Protocol"]);
        }

        private static KnowledgeBaseData BuildKnowledgeBase(params string[] genericTypeIds)
        {
            KnowledgeBaseData knowledgeBase = new KnowledgeBaseData();
            foreach (string id in genericTypeIds)
            {
                knowledgeBase.GenericElements.Add(new ElementType { Id = id });
            }

            return knowledgeBase;
        }

        private static (ThreatModel Model, Connector Flow) ModelWithFlow()
        {
            Connector flow = new Connector { Guid = Guid.NewGuid(), GenericTypeId = "GE.DF" };
            DrawingSurfaceModel surface = new DrawingSurfaceModel { Guid = Guid.NewGuid() };
            surface.Lines[flow.Guid] = flow;
            ThreatModel model = new ThreatModel();
            model.DrawingSurfaceList.Add(surface);
            return (model, flow);
        }

        private static (ThreatModel Model, StencilParallelLines Store) ModelWithStore()
        {
            StencilParallelLines store = new StencilParallelLines { Guid = Guid.NewGuid(), GenericTypeId = "GE.DS" };
            DrawingSurfaceModel surface = new DrawingSurfaceModel { Guid = Guid.NewGuid() };
            surface.Borders[store.Guid] = store;
            ThreatModel model = new ThreatModel();
            model.DrawingSurfaceList.Add(surface);
            return (model, store);
        }
    }
}
