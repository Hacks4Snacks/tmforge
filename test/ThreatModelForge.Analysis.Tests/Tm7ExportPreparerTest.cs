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

        /// <summary>
        /// Verifies that a surface whose elements sit below the tool's minimum drawing coordinate is
        /// translated as a whole: the lowest element lands on the minimum, the relative layout is
        /// preserved, and connectors move with their endpoints so they stay attached.
        /// </summary>
        [TestMethod]
        public void PrepareShiftsSurfaceAboveTheToolMinimum()
        {
            StencilEllipse process = new StencilEllipse { Guid = Guid.NewGuid(), Left = 400, Top = -16, Width = 100, Height = 60 };
            StencilRectangle actor = new StencilRectangle { Guid = Guid.NewGuid(), Left = 80, Top = 0, Width = 120, Height = 60 };
            Connector flow = new Connector
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = "GE.DF",
                SourceGuid = actor.Guid,
                TargetGuid = process.Guid,
                SourceX = 200,
                SourceY = 4,
                TargetX = 400,
                TargetY = -6,
                HandleX = 300,
                HandleY = -1,
            };

            DrawingSurfaceModel surface = new DrawingSurfaceModel { Guid = Guid.NewGuid() };
            surface.Borders[process.Guid] = process;
            surface.Borders[actor.Guid] = actor;
            surface.Lines[flow.Guid] = flow;
            ThreatModel model = new ThreatModel();
            model.DrawingSurfaceList.Add(surface);

            Tm7ExportPreparer.Prepare(model);

            // The lowest coordinate was the process top (-16); a whole-surface shift of +26 lands it on
            // the minimum while preserving the 16px gap above the actor.
            Assert.AreEqual(10, process.Top);
            Assert.AreEqual(26, actor.Top);

            // The horizontal minimum (80) was already valid, so x is unchanged.
            Assert.AreEqual(400, process.Left);
            Assert.AreEqual(80, actor.Left);

            // The connector's endpoints and handle move with the surface.
            Assert.AreEqual(30, flow.SourceY);
            Assert.AreEqual(20, flow.TargetY);
            Assert.AreEqual(25, flow.HandleY);
            Assert.AreEqual(200, flow.SourceX);
            Assert.AreEqual(400, flow.TargetX);
        }

        /// <summary>
        /// Verifies that a surface already above the minimum is left in place.
        /// </summary>
        [TestMethod]
        public void PrepareLeavesValidCoordinatesUnchanged()
        {
            StencilEllipse process = new StencilEllipse { Guid = Guid.NewGuid(), Left = 220, Top = 60, Width = 100, Height = 60 };
            DrawingSurfaceModel surface = new DrawingSurfaceModel { Guid = Guid.NewGuid() };
            surface.Borders[process.Guid] = process;
            ThreatModel model = new ThreatModel();
            model.DrawingSurfaceList.Add(surface);

            Tm7ExportPreparer.Prepare(model);

            Assert.AreEqual(220, process.Left);
            Assert.AreEqual(60, process.Top);
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
