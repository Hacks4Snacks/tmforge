namespace ThreatModelForge.Analysis.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;

    /// <summary>
    /// Unit tests for <see cref="KnowledgeBaseCatalog"/>.
    /// </summary>
    [TestClass]
    public class KnowledgeBaseCatalogTest
    {
        /// <summary>
        /// Verifies that the built-in default knowledge base is recognized as the default.
        /// </summary>
        [TestMethod]
        public void IsDefaultRecognizesBuiltInKnowledgeBase()
        {
            Assert.IsTrue(KnowledgeBaseCatalog.IsDefault(KnowledgeBaseCatalog.CreateDefault()));
        }

        /// <summary>
        /// Verifies that a knowledge base with a different manifest identifier is not the default.
        /// </summary>
        [TestMethod]
        public void IsDefaultRejectsForeignKnowledgeBase()
        {
            KnowledgeBaseData foreign = new KnowledgeBaseData
            {
                Manifest = new Manifest { Id = Guid.NewGuid(), Name = "Third Party" },
            };

            Assert.IsFalse(KnowledgeBaseCatalog.IsDefault(foreign));
        }

        /// <summary>
        /// Verifies that a knowledge base without a manifest is not the default.
        /// </summary>
        [TestMethod]
        public void IsDefaultRejectsKnowledgeBaseWithoutManifest()
        {
            Assert.IsFalse(KnowledgeBaseCatalog.IsDefault(new KnowledgeBaseData()));
        }

        /// <summary>
        /// Verifies that a null knowledge base is rejected.
        /// </summary>
        [TestMethod]
        public void IsDefaultThrowsForNull()
        {
            Assert.Throws<ArgumentNullException>(() => KnowledgeBaseCatalog.IsDefault(null!));
        }

        /// <summary>
        /// Verifies that the default knowledge base declares a standard element per authoring stencil,
        /// each parented to a declared generic element type.
        /// </summary>
        [TestMethod]
        public void CreateDefaultDeclaresStandardElementsParentedToGenerics()
        {
            KnowledgeBaseData knowledgeBase = KnowledgeBaseCatalog.CreateDefault();

            Assert.IsTrue(knowledgeBase.StandardElements.Count > 0);
            HashSet<string> genericIds = new HashSet<string>(
                knowledgeBase.GenericElements.Select(element => element.Id!),
                StringComparer.Ordinal);
            foreach (ElementType standard in knowledgeBase.StandardElements)
            {
                Assert.IsTrue(
                    genericIds.Contains(standard.ParentId!),
                    "Standard element '" + standard.Id + "' has an unknown parent '" + standard.ParentId + "'.");
            }

            Assert.IsTrue(knowledgeBase.StandardElements.Any(element => element.Id == "azure-sql"));
        }
    }
}
