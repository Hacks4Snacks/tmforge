namespace ThreatModelForge.Editing.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for <see cref="StencilSubtypeProjection"/>.
    /// </summary>
    [TestClass]
    public class StencilSubtypeProjectionTest
    {
        /// <summary>
        /// Verifies that a null model is rejected.
        /// </summary>
        [TestMethod]
        public void ApplyThrowsForNullModel()
        {
            Assert.Throws<ArgumentNullException>(() => StencilSubtypeProjection.Apply(null!, new KnowledgeBaseData()));
        }

        /// <summary>
        /// Verifies that a null knowledge base is rejected.
        /// </summary>
        [TestMethod]
        public void ApplyThrowsForNullKnowledgeBase()
        {
            Assert.Throws<ArgumentNullException>(() => StencilSubtypeProjection.Apply(new ThreatModel(), null!));
        }

        /// <summary>
        /// Verifies that an element placed from a stencil is retyped to the matching standard element,
        /// keeping its generic type.
        /// </summary>
        [TestMethod]
        public void ApplyRetypesElementToDeclaredSubtype()
        {
            (ThreatModel model, StencilParallelLines element) = ModelWithStore("azure-sql");

            StencilSubtypeProjection.Apply(model, KnowledgeBaseWithSubtype("azure-sql"));

            Assert.AreEqual("azure-sql", element.TypeId);
            Assert.AreEqual("GE.DS", element.GenericTypeId);
        }

        /// <summary>
        /// Verifies that an element without a stencil property is left untouched.
        /// </summary>
        [TestMethod]
        public void ApplyLeavesElementWithoutStencilType()
        {
            (ThreatModel model, StencilParallelLines element) = ModelWithStore(null);

            StencilSubtypeProjection.Apply(model, KnowledgeBaseWithSubtype("azure-sql"));

            Assert.AreEqual("GE.DS", element.TypeId);
        }

        /// <summary>
        /// Verifies that an element whose stencil is not declared as a standard element is left
        /// untouched, so a retyped element always resolves in the tool.
        /// </summary>
        [TestMethod]
        public void ApplyLeavesUndeclaredStencilType()
        {
            (ThreatModel model, StencilParallelLines element) = ModelWithStore("mystery-store");

            StencilSubtypeProjection.Apply(model, KnowledgeBaseWithSubtype("azure-sql"));

            Assert.AreEqual("GE.DS", element.TypeId);
        }

        private static (ThreatModel Model, StencilParallelLines Element) ModelWithStore(string? stencilType)
        {
            StencilParallelLines element = new StencilParallelLines { Guid = Guid.NewGuid(), GenericTypeId = "GE.DS", TypeId = "GE.DS" };
            if (stencilType != null)
            {
                DiagramElementHelper.SetCustomProperty(element, "StencilType", stencilType);
            }

            DrawingSurfaceModel surface = new DrawingSurfaceModel { Guid = Guid.NewGuid() };
            surface.Borders[element.Guid] = element;
            ThreatModel model = new ThreatModel();
            model.DrawingSurfaceList.Add(surface);
            return (model, element);
        }

        private static KnowledgeBaseData KnowledgeBaseWithSubtype(string id)
        {
            KnowledgeBaseData knowledgeBase = new KnowledgeBaseData();
            knowledgeBase.StandardElements.Add(new ElementType { Id = id, ParentId = "GE.DS" });
            return knowledgeBase;
        }
    }
}
