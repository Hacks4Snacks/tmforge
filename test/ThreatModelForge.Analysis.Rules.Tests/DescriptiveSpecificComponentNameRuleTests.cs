namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the <see cref="DescriptiveSpecificComponentNameRule"/> class.
    /// </summary>
    [TestClass]
    public class DescriptiveSpecificComponentNameRuleTests
    {
        /// <summary>
        /// Gets or sets the test context.
        /// </summary>
        public TestContext? TestContext { get; set; }

        /// <summary>
        /// Unit test for the <see cref="DescriptiveSpecificComponentNameRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using var target = new DescriptiveSpecificComponentNameRule();
            Assert.AreEqual($"TM{RuleIDs.DescriptiveSpecificComponentNameRule}", target.ID);
            Assert.AreEqual(MessageSeverity.Warning, target.Severity);
        }

        /// <summary>
        /// Unit test for the <see cref="DescriptiveSpecificComponentNameRule.Evaluate(RuleEvaluationContext)"/> method.
        /// </summary>
        [TestMethod]
        public void EvaluateTest()
        {
            StencilRectangle b1 = new StencilRectangle
            {
                Guid = Guid.NewGuid(),
                TypeId = "GE.EI.FOO",
                GenericTypeId = "GE.EI",
                Properties =
                {
                    new HeaderDisplayAttribute()
                    {
                        Name = "Foo Component",
                        DisplayName = "Foo Component",
                    },
                    new StringDisplayAttribute()
                    {
                        Name = "Name",
                        DisplayName = "Name",
                        Value = "Foo Component",
                    },
                },
            };

            ThreatModel model = new ThreatModel()
            {
                DrawingSurfaceList =
                {
                    new DrawingSurfaceModel
                    {
                        Borders =
                        {
                            { b1.Guid, b1 },
                        },
                    },
                },
            };

            using var target = new DescriptiveSpecificComponentNameRule();
            MockMessageWriter writer = new MockMessageWriter();
            Dictionary<string, string> vars = new ()
            {
                {
                    GeneralPurposeComponentSet.VariableName,
                    "GE.EI.FOO"
                },
            };

            var context = new RuleEvaluationContext(model, writer, vars.AsReadOnly());
            target.Evaluate(context);
            Assert.AreEqual(1, writer.Messages.Count);
            Message actual = writer.Messages[0];
            Assert.AreEqual(target.ID, actual.Source!.ID);
            Assert.AreEqual(b1.Guid, actual.Target!.Guid);
            Assert.IsFalse(string.IsNullOrWhiteSpace(actual.Text));
        }

        /// <summary>
        /// Unit test that flags all default general purpose type ids in the azure and default templates.
        /// </summary>
        [TestMethod]
        public void DefaultStencilsInScopeTest()
        {
            ThreatModel model = new ThreatModel();
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Header = "DFD-0" };
            model.DrawingSurfaceList.Add(diagram);

            // One component per default general-purpose stencil, each carrying the stencil's
            // default (type) name so every one is flagged by the rule.
            foreach (StencilRectangle component in GeneralPurposeComponentSet.Default.TypeIds.Select(typeId => new StencilRectangle
            {
                Guid = Guid.NewGuid(),
                TypeId = typeId,
                GenericTypeId = "GE.P",
                Properties =
                {
                    new HeaderDisplayAttribute { Name = "Default Name", DisplayName = "Default Name" },
                    new StringDisplayAttribute { Name = "Name", DisplayName = "Name", Value = "Default Name" },
                },
            }))
            {
                diagram.Borders.Add(component.Guid, component);
            }

            using var target = new DescriptiveSpecificComponentNameRule();
            MockMessageWriter writer = new ();
            Dictionary<string, string> vars = new ()
            {
            };

            var context = new RuleEvaluationContext(model, writer, vars.AsReadOnly());
            target.Evaluate(context);
            Assert.AreEqual(GeneralPurposeComponentSet.Default.TypeIds.Count, writer.Messages.Count);
        }
    }
}
