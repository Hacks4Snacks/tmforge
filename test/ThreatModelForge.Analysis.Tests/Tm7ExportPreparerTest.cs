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

        /// <summary>A foreign category cannot silently reuse an effective category id with another name.</summary>
        [TestMethod]
        public void PrepareRejectsConflictingForeignThreatCategory()
        {
            ThreatModel model = ModelWithFlow("Protocol", "HTTPS");
            model.KnowledgeBase = new KnowledgeBaseData
            {
                Manifest = new Manifest { Id = Guid.NewGuid(), Name = "Third Party" },
                ThreatCategories =
                {
                    new ThreatCategory { Id = "S", Name = "Safety" },
                },
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => Tm7ExportPreparer.Prepare(model));

            StringAssert.Contains(exception.Message, "category 'S'");
        }

        /// <summary>A foreign threat type cannot silently bind a rule id to different metadata.</summary>
        [TestMethod]
        public void PrepareRejectsConflictingForeignThreatType()
        {
            ThreatModel model = ModelWithFlow("Protocol", "HTTPS");
            model.KnowledgeBase = new KnowledgeBaseData
            {
                Manifest = new Manifest { Id = Guid.NewGuid(), Name = "Third Party" },
                ThreatTypes =
                {
                    new ThreatType
                    {
                        Id = "TM1023",
                        Category = "T",
                        ShortTitle = "Unrelated foreign threat",
                        Description = "Unrelated foreign threat",
                    },
                },
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => Tm7ExportPreparer.Prepare(model));

            StringAssert.Contains(exception.Message, "threat type 'TM1023'");
        }

        /// <summary>Foreign category spelling is retained and generated priority metadata is merged.</summary>
        [TestMethod]
        public void PrepareCanonicalizesForeignCategoryReferenceAndMergesPriority()
        {
            ThreatModel model = ModelWithFlow("Protocol", "HTTPS");
            ThreatMetaData metadata = new ThreatMetaData { IsPriorityUsed = true };
            ThreatMetaDatum nativePriority = new ThreatMetaDatum
            {
                Id = "22222222-2222-2222-2222-222222222222",
                Name = "Priority",
                Label = "Severity",
                AttributeType = 1,
            };
            nativePriority.Values.Add("High");
            nativePriority.Values.Add("Medium");
            nativePriority.Values.Add("Low");
            ThreatMetaDatum mitigation = new ThreatMetaDatum
            {
                Id = nativePriority.Id,
                Name = "PossibleMitigations",
                Label = "Possible Mitigation(s)",
                AttributeType = 2,
            };
            mitigation.Values.Add(string.Empty);
            metadata.PropertiesMetaData.Add(mitigation);
            metadata.PropertiesMetaData.Add(nativePriority);
            model.KnowledgeBase = new KnowledgeBaseData
            {
                Manifest = new Manifest { Id = Guid.NewGuid(), Name = "Third Party" },
                ThreatCategories =
                {
                    new ThreatCategory { Id = "MEDICAL/PRIVACY", Name = "Privacy" },
                },
                ThreatMetaData = metadata,
            };
            ThreatType existingType = new ThreatType
            {
                Id = "medical/PRIV-1",
                Category = "MEDICAL/PRIVACY",
                ShortTitle = "Privacy risk",
                Description = "Minimize retained data.",
            };
            ThreatMetaDatum stalePriority = new ThreatMetaDatum
            {
                Id = "tmforge:priority",
                Name = "Priority",
                Label = "Priority",
                AttributeType = 1,
            };
            stalePriority.Values.Add("High");
            existingType.PropertiesMetaData.Add(stalePriority);
            model.KnowledgeBase.ThreatTypes.Add(existingType);
            using RuleSet rules = new RuleSet();
            rules.Rules.Add(new PriorityRule());

            Tm7ExportPreparer.Prepare(model, rules);

            ThreatType injectedType = model.KnowledgeBase.ThreatTypes.Single(
                threat => threat.Id == "medical/PRIV-1");
            Assert.AreEqual("MEDICAL/PRIVACY", injectedType.Category);
            ThreatMetaDatum priority = injectedType.PropertiesMetaData.Single();
            Assert.AreEqual(nativePriority.Id, priority.Id);
            Assert.AreEqual(nativePriority.Name, priority.Name);
            Assert.AreEqual(nativePriority.Label, priority.Label);
            Assert.AreEqual(nativePriority.HideFromUI, priority.HideFromUI);
            Assert.AreEqual(nativePriority.AttributeType, priority.AttributeType);
            Assert.AreEqual("High", priority.Values.Single());
            Assert.IsTrue(model.KnowledgeBase.ThreatCategories.Any(category => category.Id == injectedType.Category));
            Assert.HasCount(2, model.KnowledgeBase.ThreatMetaData!.PropertiesMetaData);
            Assert.AreSame(nativePriority, model.KnowledgeBase.ThreatMetaData.PropertiesMetaData.Single(
                datum => datum.Name == "Priority"));
        }

        /// <summary>Foreign global priority metadata cannot silently redefine the generated catalog.</summary>
        [TestMethod]
        public void PrepareRejectsConflictingForeignPriorityMetadata()
        {
            ThreatModel model = ModelWithFlow("Protocol", "HTTPS");
            ThreatMetaData metadata = new ThreatMetaData { IsPriorityUsed = true };
            ThreatMetaDatum priority = new ThreatMetaDatum
            {
                Id = "tmforge:priority",
                Name = "Priority",
                Label = "Priority",
                Description = "Different definition.",
                AttributeType = 1,
            };
            priority.Values.Add("Urgent");
            metadata.PropertiesMetaData.Add(priority);
            model.KnowledgeBase = new KnowledgeBaseData
            {
                Manifest = new Manifest { Id = Guid.NewGuid(), Name = "Third Party" },
                ThreatMetaData = metadata,
            };
            using RuleSet rules = new RuleSet();
            rules.Rules.Add(new PriorityRule());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => Tm7ExportPreparer.Prepare(model, rules));

            StringAssert.Contains(exception.Message, "global threat metadata");
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

        private sealed class PriorityRule : Rule
        {
            /// <summary>Initializes a new instance of the <see cref="PriorityRule"/> class.</summary>
            public PriorityRule()
                : base("medical/PRIV-1", MessageSeverity.Info, "medical")
            {
                this.FullDescription = "Privacy risk";
                this.HelpText = "Minimize retained data.";
            }

            /// <inheritdoc/>
            public override RuleThreatCategory ThreatCategory { get; } = new RuleThreatCategory(
                "medical/privacy",
                "privacy",
                "Privacy",
                null,
                null);

            /// <inheritdoc/>
            public override ThreatPriority? DefaultThreatPriority => ThreatPriority.High;

            /// <inheritdoc/>
            public override void Evaluate(RuleEvaluationContext context)
            {
            }
        }
    }
}
