namespace ThreatModelForge.Editing.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Editing;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for <see cref="DiagramElementHelper"/>.
    /// </summary>
    [TestClass]
    public class DiagramElementHelperTest
    {
        /// <summary>
        /// Verifies that an element without a name returns an empty string.
        /// </summary>
        [TestMethod]
        public void GetNameReturnsEmptyWhenAbsent()
        {
            StencilEllipse element = new StencilEllipse { Guid = Guid.NewGuid() };

            Assert.AreEqual(string.Empty, DiagramElementHelper.GetName(element));
        }

        /// <summary>
        /// Verifies that setting a name adds a property when none exists and updates it thereafter.
        /// </summary>
        [TestMethod]
        public void SetNameAddsThenUpdatesNameProperty()
        {
            StencilRectangle element = new StencilRectangle { Guid = Guid.NewGuid() };

            DiagramElementHelper.SetName(element, "First");
            Assert.AreEqual("First", DiagramElementHelper.GetName(element));

            DiagramElementHelper.SetName(element, "Second");
            Assert.AreEqual("Second", DiagramElementHelper.GetName(element));
        }

        /// <summary>
        /// Verifies that reading the name of a null element returns an empty string.
        /// </summary>
        [TestMethod]
        public void GetNameReturnsEmptyForNull()
        {
            Assert.AreEqual(string.Empty, DiagramElementHelper.GetName(null!));
        }
    }
}
