namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for the <see cref="MissingAnyExternalInteractorsRule"/> class.
    /// </summary>
    [TestClass]
    public class MissingAnyExternalInteractorsRuleTests
    {
        /// <summary>
        /// Gets or sets the test context.
        /// </summary>
        public TestContext? TestContext { get; set; }

        /// <summary>
        /// Unit test for the <see cref="MissingAnyExternalInteractorsRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using (MissingAnyExternalInteractorsRule target = new MissingAnyExternalInteractorsRule())
            {
                Assert.IsNotNull(target.ID);
                Assert.AreEqual(MessageSeverity.Warning, target.Severity);
                Assert.IsNotNull(target.HelpUri);
                Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
                Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
            }
        }

        /// <summary>
        /// Unit test for the <see cref="MissingAnyExternalInteractorsRule.Evaluate(RuleEvaluationContext)"/> method.
        /// </summary>
        [TestMethod]
        public void EvaluateTest()
        {
            StencilRectangle browser = new StencilRectangle
            {
                Guid = Guid.NewGuid(),
                TypeId = "SE.EI.TMCore.Browser",
                GenericTypeId = "GE.EI",
                Properties =
                {
                    new HeaderDisplayAttribute { Name = "Browser", DisplayName = "Browser" },
                    new StringDisplayAttribute { Name = "Name", DisplayName = "Name", Value = "Browser" },
                },
            };
            StencilEllipse process = new StencilEllipse
            {
                Guid = Guid.NewGuid(),
                TypeId = "GE.P",
                GenericTypeId = "GE.P",
                Properties =
                {
                    new HeaderDisplayAttribute { Name = "Generic Process", DisplayName = "Generic Process" },
                    new StringDisplayAttribute { Name = "Name", DisplayName = "Name", Value = "Process" },
                },
            };
            ThreatModel model = new ThreatModel
            {
                DrawingSurfaceList =
                {
                    new DrawingSurfaceModel
                    {
                        Header = "DFD-0",
                        Borders =
                        {
                            { browser.Guid, browser },
                            { process.Guid, process },
                        },
                    },
                },
            };

            MockMessageWriter writer = new MockMessageWriter();
            using (MissingAnyExternalInteractorsRule target = new MissingAnyExternalInteractorsRule())
            {
                RuleEvaluationContext context = new RuleEvaluationContext(model, writer);
                target.Evaluate(context);
                Assert.AreEqual(0, writer.Messages.Count);

                Entity? external = model.DrawingSurfaceList[0].ExternalInteractors().FirstOrDefault();
                Assert.IsNotNull(external);
                model.DrawingSurfaceList[0].Borders.Remove(external!.Guid);

                target.Evaluate(context);
                Assert.AreEqual(1, writer.Messages.Count);
            }
        }
    }
}
