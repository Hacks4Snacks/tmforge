namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for the <see cref="MinimumComponentCountRule"/> class.
    /// </summary>
    [TestClass]
    public class MinimumComponentCountRuleTests
    {
        /// <summary>
        /// Unit test for the <see cref="MinimumComponentCountRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using (MinimumComponentCountRule target = new MinimumComponentCountRule())
            {
                Assert.IsNotNull(target.ID);
                Assert.AreEqual(MessageSeverity.Warning, target.Severity);
                Assert.IsNotNull(target.HelpUri);
                Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
                Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
            }
        }

        /// <summary>
        /// Unit test for the <see cref="MinimumComponentCountRule.Evaluate(RuleEvaluationContext)"/> method.
        /// </summary>
        [TestMethod]
        public void EvaluateTest()
        {
            using (MinimumComponentCountRule target = new MinimumComponentCountRule())
            {
                MockMessageWriter writer = new MockMessageWriter();

                Entity[] components = new Entity[]
                {
                new StencilEllipse { Guid = Guid.NewGuid() },
                new StencilEllipse { Guid = Guid.NewGuid() },
                new StencilEllipse { Guid = Guid.NewGuid() },
                };

                ThreatModel model = new ThreatModel
                {
                    DrawingSurfaceList =
                {
                    new DrawingSurfaceModel
                    {
                        Header = "DFD-0",
                    },
                },
                };

                foreach (var e in components)
                {
                    model.DrawingSurfaceList[0].Borders.Add(e.Guid, e);
                }

                RuleEvaluationContext context = new RuleEvaluationContext(model, writer);
                target.Evaluate(context);
                Assert.AreEqual(0, writer.Messages.Count);

                components = new Entity[]
                {
                new StencilEllipse { Guid = Guid.NewGuid(), GenericTypeId = "GE.P" },
                new StencilEllipse { Guid = Guid.NewGuid(), GenericTypeId = "GE.P" },
                new StencilEllipse { Guid = Guid.NewGuid(), GenericTypeId = "GE.A" },
                new BorderBoundary { Guid = Guid.NewGuid() },
                };

                model.DrawingSurfaceList[0].Borders.Clear();
                foreach (var e in components)
                {
                    model.DrawingSurfaceList[0].Borders.Add(e.Guid, e);
                }

                context = new RuleEvaluationContext(model, writer);
                target.Evaluate(context);
                Assert.AreEqual(1, writer.Messages.Count);
                Assert.IsTrue(writer.Messages[0].Model != null);
            }
        }
    }
}
