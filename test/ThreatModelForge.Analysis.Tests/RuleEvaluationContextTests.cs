namespace ThreatModelForge.Analysis.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for the <see cref="RuleEvaluationContext"/> class.
    /// </summary>
    [TestClass]
    public class RuleEvaluationContextTests
    {
        /// <summary>
        /// Gets or sets the test context.
        /// </summary>
        public TestContext? TestContext
        {
            get;
            set;
        }

        /// <summary>
        /// Unit test for the <see cref="RuleEvaluationContext"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            RuleEvaluationContext target = new RuleEvaluationContext(
                new ThreatModel(),
                new MockMessageWriter());
            Assert.IsNotNull(target.Model);
            Assert.IsNotNull(target.Writer);
            Assert.IsNotNull(target.Variables);
            Assert.AreEqual(0, target.Variables.Count);

            Dictionary<string, string> variables = new Dictionary<string, string>
            {
                { "Foo", "123" },
                { "Bar", "abc" },
            };

            target = new RuleEvaluationContext(
                new ThreatModel(),
                new MockMessageWriter(),
                variables);
            Assert.AreEqual(variables.Count, target.Variables.Count);
            Assert.AreEqual(variables["Foo"], target.Variables["FOO"]);
            Assert.AreEqual(variables["Bar"], target.Variables["BAR"]);
        }

        /// <summary>
        /// Unit test for the <see cref="RuleEvaluationContext"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorNegativeTest()
        {
            RuleEvaluationContext? target = null;
            ThreatModel? model = null;
            MessageWriter? messageWriter = null;

            try
            {
                target = new RuleEvaluationContext(model!, messageWriter!);
                Assert.Fail("Expected exception.");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("model", ex.ParamName);
            }

            try
            {
                target = new RuleEvaluationContext(new ThreatModel(), messageWriter!);
                Assert.Fail("Expected exception.");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("writer", ex.ParamName);
            }
        }

        /// <summary>
        /// Unit test for the <see cref="RuleEvaluationContext.ApplySuppressions(IEnumerable{SuppressMessage}, RuleSet)"/> method.
        /// </summary>
        [TestMethod]
        public void ApplySuppressionsTest()
        {
            MockMessageWriter writer = new MockMessageWriter();
            RuleEvaluationContext target = new RuleEvaluationContext(
                new ThreatModel(),
                writer);
            IEnumerable<SuppressMessage>? items = null;
            RuleSet? nullRuleSet = null;

            try
            {
                target.ApplySuppressions(items!, nullRuleSet!);
                Assert.Fail("Expected exception.");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("items", ex.ParamName);
            }

            try
            {
                target.ApplySuppressions(Array.Empty<SuppressMessage>(), nullRuleSet!);
                Assert.Fail("Expected exception.");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("ruleSet", ex.ParamName);
            }

            using (RuleSet ruleSet = new RuleSet())
            {
                ruleSet.Rules.Add(new Rule1());
                ruleSet.Rules.Add(new Rule2());

                SuppressMessage suppression1 = new SuppressMessage
                {
                    RuleID = ruleSet.Rules[0].ID,
                    Justification = "Test",
                };

                SuppressMessage suppression2 = new SuppressMessage
                {
                    RuleID = ruleSet.Rules[1].ID,
                    Justification = "Not a valid suppression.",
                    Model = "Not found",
                };

                target.ApplySuppressions(new[] { suppression1, suppression2 }, ruleSet);
                Assert.IsTrue(target.Suppressions.Contains(suppression1));
                Assert.IsNotNull(suppression1.ResolvedRule);
                Assert.IsFalse(target.Suppressions.Contains(suppression2));
                Assert.IsTrue(writer.CoreMessages.Count > 0);
            }
        }

        /// <summary>
        /// Unit test for the <see cref="RuleEvaluationContext.GenerateListing"/> method.
        /// </summary>
        [TestMethod]
        public void GenerateListingTest()
        {
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = Guid.NewGuid(), Header = "DFD-0" };

            void Configure(Entity entity, string typeId, string name)
            {
                entity.Guid = Guid.NewGuid();
                entity.TypeId = typeId;
                entity.Properties.Add(new HeaderDisplayAttribute { Name = name, DisplayName = name });
                entity.Properties.Add(new StringDisplayAttribute { Name = "Name", DisplayName = "Name", Value = name });
            }

            StencilEllipse process = new StencilEllipse { GenericTypeId = "GE.P" };
            Configure(process, "SE.P.TMCore.WebApp", "Web Process");
            StencilRectangle external = new StencilRectangle { GenericTypeId = "GE.EI" };
            Configure(external, "SE.EI.TMCore.Browser", "Browser");
            StencilParallelLines store = new StencilParallelLines { GenericTypeId = "GE.DS" };
            Configure(store, "SE.DS.TMCore.SQL", "Database");
            diagram.Borders.Add(process.Guid, process);
            diagram.Borders.Add(external.Guid, external);
            diagram.Borders.Add(store.Guid, store);

            BorderBoundary boundary1 = new BorderBoundary { GenericTypeId = "GE.TB.B" };
            Configure(boundary1, "GE.TB.B", "Internet Boundary");
            BorderBoundary boundary2 = new BorderBoundary { GenericTypeId = "GE.TB.B" };
            Configure(boundary2, "GE.TB.B", "Service Boundary");
            diagram.Borders.Add(boundary1.Guid, boundary1);
            diagram.Borders.Add(boundary2.Guid, boundary2);

            Connector edge1 = new Connector { GenericTypeId = "GE.DF", SourceGuid = process.Guid, TargetGuid = external.Guid };
            Configure(edge1, "GE.DF", "Request");
            Connector edge2 = new Connector { GenericTypeId = "GE.DF", SourceGuid = external.Guid, TargetGuid = store.Guid };
            Configure(edge2, "GE.DF", "Query");
            diagram.Lines.Add(edge1.Guid, edge1);
            diagram.Lines.Add(edge2.Guid, edge2);

            ThreatModel model = new ThreatModel { DrawingSurfaceList = { diagram } };
            MockMessageWriter writer = new MockMessageWriter();
            RuleEvaluationContext target = new RuleEvaluationContext(
                model,
                writer);

            ModelListing actual = target.GenerateListing();
            Assert.IsNotNull(actual);
            Assert.AreEqual(2, actual.TrustBoundaries.Count);
            foreach (TrustBoundaryListing l in actual.TrustBoundaries)
            {
                Assert.AreEqual("DFD-0", l.DiagramHeader);
                Assert.AreNotEqual(Guid.Empty, l.DiagramID);
                Assert.AreNotEqual(Guid.Empty, l.ID);
                Assert.IsFalse(string.IsNullOrEmpty(l.Name));
                Assert.IsFalse(string.IsNullOrEmpty(l.HeaderName));
                Assert.IsFalse(string.IsNullOrEmpty(l.TypeID));
            }

            Assert.AreEqual(3, actual.Components.Count);
            foreach (ComponentListing l in actual.Components)
            {
                Assert.AreEqual("DFD-0", l.DiagramHeader);
                Assert.AreNotEqual(Guid.Empty, l.DiagramID);
                Assert.AreNotEqual(Guid.Empty, l.ID);
                Assert.IsFalse(string.IsNullOrEmpty(l.Name));
                Assert.IsFalse(string.IsNullOrEmpty(l.HeaderName));
                Assert.IsFalse(string.IsNullOrEmpty(l.TypeID));
            }

            Assert.AreEqual(2, actual.Connectors.Count);
            foreach (ConnectorListing l in actual.Connectors)
            {
                Assert.AreEqual("DFD-0", l.DiagramHeader);
                Assert.AreNotEqual(Guid.Empty, l.DiagramID);
                Assert.AreNotEqual(Guid.Empty, l.ID);
                Assert.IsFalse(string.IsNullOrEmpty(l.Name));
                Assert.IsFalse(string.IsNullOrEmpty(l.HeaderName));
                Assert.IsFalse(string.IsNullOrEmpty(l.TypeID));
                Assert.AreNotEqual(Guid.Empty, l.SourceComponentID);
                Assert.AreNotEqual(Guid.Empty, l.TargetComponentID);
            }
        }
    }
}
