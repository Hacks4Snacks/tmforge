namespace ThreatModelForge.Editing.Tests
{
    using System;
    using System.Linq;
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

        /// <summary>
        /// Verifies that a typed list (drop-down) property is surfaced by its display name, as though
        /// it were a custom property, so a model imported from a typed <c>.tm7</c> feeds the rules.
        /// </summary>
        [TestMethod]
        public void GetCustomPropertiesSurfacesTypedListProperty()
        {
            StencilRectangle element = new StencilRectangle { Guid = Guid.NewGuid() };
            element.Properties.Add(new ListDisplayAttribute
            {
                Name = "Protocol",
                DisplayName = "Protocol",
                Value = new[] { "Select", "HTTPS", "HTTP" },
                SelectedIndex = 1,
            });

            Assert.AreEqual("HTTPS", DiagramElementHelper.GetCustomProperties(element)["Protocol"]);
        }

        /// <summary>
        /// Verifies that a typed list property left at the unset sentinel is treated as absent.
        /// </summary>
        [TestMethod]
        public void GetCustomPropertiesOmitsUnsetTypedListProperty()
        {
            StencilRectangle element = new StencilRectangle { Guid = Guid.NewGuid() };
            element.Properties.Add(new ListDisplayAttribute
            {
                Name = "Protocol",
                DisplayName = "Protocol",
                Value = new[] { "Select", "HTTPS", "HTTP" },
                SelectedIndex = 0,
            });

            Assert.IsFalse(DiagramElementHelper.GetCustomProperties(element).ContainsKey("Protocol"));
        }

        /// <summary>
        /// Verifies that a custom value takes precedence over a typed list value for the same key.
        /// </summary>
        [TestMethod]
        public void GetCustomPropertiesPrefersCustomOverTypedForSameKey()
        {
            StencilRectangle element = new StencilRectangle { Guid = Guid.NewGuid() };
            DiagramElementHelper.SetCustomProperty(element, "Protocol", "TLS");
            element.Properties.Add(new ListDisplayAttribute
            {
                Name = "Protocol",
                DisplayName = "Protocol",
                Value = new[] { "Select", "HTTPS", "HTTP" },
                SelectedIndex = 1,
            });

            Assert.AreEqual("TLS", DiagramElementHelper.GetCustomProperties(element)["Protocol"]);
        }

        /// <summary>
        /// Verifies that setting a property the tool has already typed moves that typed selection
        /// rather than adding a custom attribute beside it. Leaving both would give the element two
        /// answers to the same question, and whichever the reader reached first would win.
        /// </summary>
        [TestMethod]
        public void SetCustomPropertyMovesAnExistingTypedSelection()
        {
            StencilRectangle element = TypedElement();

            DiagramElementHelper.SetCustomProperty(element, "Protocol", "HTTP");

            Assert.AreEqual("HTTP", DiagramElementHelper.GetCustomProperties(element)["Protocol"]);
            Assert.AreEqual(1, element.Properties.OfType<ListDisplayAttribute>().Count());
            Assert.AreEqual(0, element.Properties.OfType<CustomStringDisplayAttribute>().Count());
        }

        /// <summary>
        /// Verifies that a value the typed property does not offer is still stored, as a custom
        /// attribute, so an authored value is preserved rather than silently dropped.
        /// </summary>
        [TestMethod]
        public void SetCustomPropertyFallsBackToACustomAttributeForAnUnknownValue()
        {
            StencilRectangle element = TypedElement();

            DiagramElementHelper.SetCustomProperty(element, "Protocol", "QUIC");

            Assert.AreEqual("QUIC", DiagramElementHelper.GetCustomProperties(element)["Protocol"]);
            Assert.AreEqual(1, element.Properties.OfType<CustomStringDisplayAttribute>().Count());
        }

        /// <summary>
        /// Verifies that moving a typed selection clears a custom attribute previously written for the
        /// same key. A custom value outranks a typed one when read, so a stale one left behind would
        /// override the value just set.
        /// </summary>
        [TestMethod]
        public void SetCustomPropertyClearsAStaleCustomValueWhenTheTypedSelectionMoves()
        {
            StencilRectangle element = TypedElement();
            DiagramElementHelper.SetCustomProperty(element, "Protocol", "QUIC");

            DiagramElementHelper.SetCustomProperty(element, "Protocol", "HTTP");

            Assert.AreEqual("HTTP", DiagramElementHelper.GetCustomProperties(element)["Protocol"]);
            Assert.AreEqual(0, element.Properties.OfType<CustomStringDisplayAttribute>().Count());
        }

        /// <summary>
        /// Verifies that a property with no typed counterpart still round-trips as a custom attribute.
        /// </summary>
        [TestMethod]
        public void SetCustomPropertyStillUpdatesAnUntypedProperty()
        {
            StencilRectangle element = new StencilRectangle { Guid = Guid.NewGuid() };

            DiagramElementHelper.SetCustomProperty(element, "Port", "443");
            DiagramElementHelper.SetCustomProperty(element, "Port", "8443");

            Assert.AreEqual("8443", DiagramElementHelper.GetCustomProperties(element)["Port"]);
            Assert.AreEqual(1, element.Properties.OfType<CustomStringDisplayAttribute>().Count());
        }

        private static StencilRectangle TypedElement()
        {
            StencilRectangle element = new StencilRectangle { Guid = Guid.NewGuid() };
            element.Properties.Add(new ListDisplayAttribute
            {
                Name = "Protocol",
                DisplayName = "Protocol",
                Value = new[] { "Select", "HTTPS", "HTTP" },
                SelectedIndex = 1,
            });

            return element;
        }
    }
}
