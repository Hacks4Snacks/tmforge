namespace ThreatModelForge.Core.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the document scaffolding that <see cref="ThreatModel.Save(System.IO.Stream)"/>
    /// applies so that a freshly authored model opens in the Microsoft Threat Modeling Tool. The
    /// scaffolding is observed through the public save API rather than the private method that
    /// performs it.
    /// </summary>
    [TestClass]
    public class ThreatModelScaffoldingTests
    {
        /// <summary>
        /// A freshly authored model with no version is stamped with the current document-format version.
        /// </summary>
        [TestMethod]
        public void SaveStampsDocumentVersionOnFreshModel()
        {
            ThreatModel model = new ThreatModel();
            model.DrawingSurfaceList.Add(Surface("Sample"));
            Assert.IsNull(model.Version);

            Scaffold(model);

            Assert.AreEqual("4.3", model.Version);
        }

        /// <summary>
        /// A legacy "1.0" document version is normalized to the current document-format version.
        /// </summary>
        [TestMethod]
        public void SaveNormalizesLegacyDocumentVersion()
        {
            ThreatModel model = new ThreatModel { Version = "1.0" };
            model.DrawingSurfaceList.Add(Surface("Sample"));

            Scaffold(model);

            Assert.AreEqual("4.3", model.Version);
        }

        /// <summary>
        /// A recognized (non-legacy) document version set on a loaded model is preserved.
        /// </summary>
        [TestMethod]
        public void SavePreservesRecognizedDocumentVersion()
        {
            ThreatModel model = new ThreatModel { Version = "4.2" };
            model.DrawingSurfaceList.Add(Surface("Sample"));

            Scaffold(model);

            Assert.AreEqual("4.2", model.Version);
        }

        /// <summary>
        /// The document-level metadata block the tool reads without a null guard is created with empty
        /// (non-null) string fields.
        /// </summary>
        [TestMethod]
        public void SavePopulatesMetaInformationDefaults()
        {
            ThreatModel model = new ThreatModel();
            model.DrawingSurfaceList.Add(Surface("Sample"));
            Assert.IsNull(model.MetaInformation);

            Scaffold(model);

            Assert.IsNotNull(model.MetaInformation);
            Assert.AreEqual(string.Empty, model.MetaInformation!.ThreatModelName);
            Assert.AreEqual(string.Empty, model.MetaInformation.Owner);
            Assert.AreEqual(string.Empty, model.MetaInformation.Contributors);
            Assert.AreEqual(string.Empty, model.MetaInformation.Reviewer);
            Assert.AreEqual(string.Empty, model.MetaInformation.HighLevelSystemDescription);
            Assert.AreEqual(string.Empty, model.MetaInformation.Assumptions);
            Assert.AreEqual(string.Empty, model.MetaInformation.ExternalDependencies);
        }

        /// <summary>
        /// Metadata already present on the model is preserved; only unset members are defaulted.
        /// </summary>
        [TestMethod]
        public void SavePreservesExistingMetaInformation()
        {
            ThreatModel model = new ThreatModel
            {
                MetaInformation = new MetaInformation { ThreatModelName = "My Model" },
            };
            model.DrawingSurfaceList.Add(Surface("Sample"));

            Scaffold(model);

            Assert.AreEqual("My Model", model.MetaInformation!.ThreatModelName);
            Assert.AreEqual(string.Empty, model.MetaInformation.Owner);
        }

        /// <summary>
        /// A profile with a prompted knowledge-base version block is created when absent.
        /// </summary>
        [TestMethod]
        public void SaveCreatesProfileWithPromptedKnowledgeBaseVersions()
        {
            ThreatModel model = new ThreatModel();
            model.DrawingSurfaceList.Add(Surface("Sample"));
            Assert.IsNull(model.Profile);

            Scaffold(model);

            Assert.IsNotNull(model.Profile);
            Assert.IsNotNull(model.Profile!.PromptedKb);
        }

        /// <summary>
        /// Each surface has its concrete and generic type identifiers defaulted to the drawing-surface
        /// type when unset.
        /// </summary>
        [TestMethod]
        public void SaveDefaultsSurfaceTypeIdentifiers()
        {
            ThreatModel model = new ThreatModel();
            DrawingSurfaceModel surface = Surface("Sample");
            model.DrawingSurfaceList.Add(surface);
            Assert.IsNull(surface.TypeId);
            Assert.IsNull(surface.GenericTypeId);

            Scaffold(model);

            Assert.AreEqual("DRAWINGSURFACE", surface.TypeId);
            Assert.AreEqual("DRAWINGSURFACE", surface.GenericTypeId);
        }

        /// <summary>
        /// The surface title (Header) is mirrored into a "Name" display property, which is the member
        /// the tool actually binds the page name to.
        /// </summary>
        [TestMethod]
        public void SaveMirrorsSurfaceHeaderIntoNameProperty()
        {
            ThreatModel model = new ThreatModel();
            DrawingSurfaceModel surface = Surface("Sample");
            model.DrawingSurfaceList.Add(surface);

            Scaffold(model);

            StringDisplayAttribute name = surface.Properties
                .OfType<StringDisplayAttribute>()
                .Single(p => string.Equals(p.DisplayName, "Name", StringComparison.Ordinal));
            Assert.AreEqual("Sample", name.Value);
        }

        /// <summary>
        /// A surface that already carries a "Name" display property is not given a second one, and the
        /// existing value is left untouched.
        /// </summary>
        [TestMethod]
        public void SaveDoesNotDuplicateExistingSurfaceName()
        {
            ThreatModel model = new ThreatModel();
            DrawingSurfaceModel surface = Surface("Sample");
            surface.Properties.Add(new StringDisplayAttribute
            {
                Name = "Name",
                DisplayName = "Name",
                Value = "Custom",
            });
            model.DrawingSurfaceList.Add(surface);

            Scaffold(model);

            Assert.AreEqual(1, NamePropertyCount(surface));
            StringDisplayAttribute name = surface.Properties
                .OfType<StringDisplayAttribute>()
                .Single(p => string.Equals(p.DisplayName, "Name", StringComparison.Ordinal));
            Assert.AreEqual("Custom", name.Value);
        }

        /// <summary>
        /// Saving a second time is a no-op for the scaffolding: the version is unchanged, no duplicate
        /// name property is added, and the metadata instance is reused rather than replaced.
        /// </summary>
        [TestMethod]
        public void SaveIsIdempotent()
        {
            ThreatModel model = new ThreatModel();
            DrawingSurfaceModel surface = Surface("Sample");
            model.DrawingSurfaceList.Add(surface);

            Scaffold(model);
            MetaInformation firstMeta = model.MetaInformation!;

            Scaffold(model);

            Assert.AreEqual("4.3", model.Version);
            Assert.AreEqual(1, NamePropertyCount(surface));
            Assert.AreSame(firstMeta, model.MetaInformation);
        }

        /// <summary>
        /// Serializes the model to a throwaway stream, applying the tool scaffolding to it in place.
        /// </summary>
        /// <param name="model">The model to save.</param>
        private static void Scaffold(ThreatModel model)
        {
            using MemoryStream stream = new MemoryStream();
            model.Save(stream);
        }

        /// <summary>
        /// Creates a drawing surface with the given title.
        /// </summary>
        /// <param name="header">The surface title.</param>
        /// <returns>The new surface.</returns>
        private static DrawingSurfaceModel Surface(string header)
        {
            return new DrawingSurfaceModel { Header = header };
        }

        /// <summary>
        /// Counts the "Name" display properties on a surface.
        /// </summary>
        /// <param name="surface">The surface to inspect.</param>
        /// <returns>The number of "Name" display properties.</returns>
        private static int NamePropertyCount(DrawingSurfaceModel surface)
        {
            return surface.Properties
                .OfType<StringDisplayAttribute>()
                .Count(p => string.Equals(p.DisplayName, "Name", StringComparison.Ordinal));
        }
    }
}
