namespace ThreatModelForge.Analysis.Tests
{
    using System;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for <see cref="Tm7ExportPreparer"/>.
    /// </summary>
    [TestClass]
    public class Tm7ExportPreparerTest
    {
        /// <summary>
        /// Verifies that a null model is rejected.
        /// </summary>
        [TestMethod]
        public void PrepareThrowsForNullModel()
        {
            Assert.Throws<ArgumentNullException>(() => Tm7ExportPreparer.Prepare(null!));
        }

        /// <summary>
        /// Verifies that a model without a knowledge base gains the default one and has its
        /// schema-backed properties typed.
        /// </summary>
        [TestMethod]
        public void PrepareEmbedsDefaultKnowledgeBaseAndTypesProperties()
        {
            ThreatModel model = ModelWithFlow("Protocol", "HTTPS");

            Tm7ExportPreparer.Prepare(model);

            Assert.IsNotNull(model.KnowledgeBase);
            Assert.IsTrue(KnowledgeBaseCatalog.IsDefault(model.KnowledgeBase!));
            Assert.IsTrue(Flow(model).Properties.OfType<ListDisplayAttribute>().Any(p => p.DisplayName == "Protocol"));
        }

        /// <summary>
        /// Verifies that a model carrying a foreign knowledge base is left untouched: the knowledge
        /// base is preserved and its properties are not retyped.
        /// </summary>
        [TestMethod]
        public void PrepareLeavesForeignKnowledgeBaseUntouched()
        {
            ThreatModel model = ModelWithFlow("Protocol", "HTTPS");
            KnowledgeBaseData foreign = new KnowledgeBaseData
            {
                Manifest = new Manifest { Id = Guid.NewGuid(), Name = "Third Party" },
            };
            model.KnowledgeBase = foreign;

            Tm7ExportPreparer.Prepare(model);

            Assert.AreSame(foreign, model.KnowledgeBase);
            Assert.IsFalse(Flow(model).Properties.OfType<ListDisplayAttribute>().Any());
        }

        /// <summary>
        /// Verifies that preparing an already-prepared model is idempotent: the knowledge base stays
        /// the default and the property is typed exactly once.
        /// </summary>
        [TestMethod]
        public void PrepareIsIdempotent()
        {
            ThreatModel model = ModelWithFlow("Protocol", "HTTPS");

            Tm7ExportPreparer.Prepare(model);
            Tm7ExportPreparer.Prepare(model);

            Assert.IsTrue(KnowledgeBaseCatalog.IsDefault(model.KnowledgeBase!));
            Assert.AreEqual(1, Flow(model).Properties.OfType<ListDisplayAttribute>().Count(p => p.DisplayName == "Protocol"));
        }

        private static ThreatModel ModelWithFlow(string key, string value)
        {
            Connector flow = new Connector { Guid = Guid.NewGuid(), GenericTypeId = "GE.DF" };
            flow.Properties.Add(new CustomStringDisplayAttribute { Value = key + ":" + value });
            DrawingSurfaceModel surface = new DrawingSurfaceModel { Guid = Guid.NewGuid() };
            surface.Lines[flow.Guid] = flow;
            ThreatModel model = new ThreatModel();
            model.DrawingSurfaceList.Add(surface);
            return model;
        }

        private static Connector Flow(ThreatModel model)
        {
            return model.DrawingSurfaceList[0].Lines.Values.OfType<Connector>().Single();
        }
    }
}
