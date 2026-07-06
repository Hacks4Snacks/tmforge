namespace ThreatModelForge.Analysis.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for the <see cref="SuppressMessage"/> class.
    /// </summary>
    [TestClass]
    public class SuppressMessageTests
    {
        /// <summary>
        /// Unit test for the <see cref="SuppressMessage.TryResolve(RuleSet, ThreatModelForge.Model.ThreatModel, MessageWriter)"/> method.
        /// </summary>
        [TestMethod]
        public void TryResolveTest()
        {
            Entity entity1 = new StencilEllipse()
            {
                Guid = Guid.NewGuid(),
                Properties =
                {
                    new HeaderDisplayAttribute
                    {
                        DisplayName = "Web Service",
                    },
                    new StringDisplayAttribute
                    {
                        Name = "Name",
                        DisplayName = "Name",
                        Value = "Expected",
                    },
                },
            };

            ThreatModel model = new ThreatModel()
            {
                DrawingSurfaceList =
                {
                    new DrawingSurfaceModel
                    {
                        Header = "DFD-0",
                        Borders =
                        {
                            { entity1.Guid, entity1 },
                        },
                    },
                },
            };

            MockMessageWriter writer = new MockMessageWriter();

            using (RuleSet ruleSet = new RuleSet()
            {
                Rules =
                {
                    new Rule1(),
                    new Rule2(),
                },
            })
            {
                SuppressMessage target = new SuppressMessage()
                {
                    RuleID = ruleSet.Rules[0].ID,
                };

                Assert.IsTrue(target.TryResolve(ruleSet, model, writer));
                Assert.AreSame(ruleSet.Rules[0], target.ResolvedRule);
                Assert.AreEqual(0, writer.CoreMessages.Count);

                target = new SuppressMessage()
                {
                    RuleID = ruleSet.Rules[0].ID,
                    Model = "DFD-0",
                };

                Assert.IsTrue(target.TryResolve(ruleSet, model, writer));
                Assert.AreSame(ruleSet.Rules[0], target.ResolvedRule);
                Assert.AreSame(model.DrawingSurfaceList[0], target.ResolvedModel);
                Assert.AreEqual(0, writer.CoreMessages.Count);

                target = new SuppressMessage()
                {
                    RuleID = ruleSet.Rules[0].ID,
                    Model = "DFD-0",
                    Target = entity1.Guid.ToString(),
                };

                Assert.IsTrue(target.TryResolve(ruleSet, model, writer));
                Assert.AreSame(ruleSet.Rules[0], target.ResolvedRule);
                Assert.AreSame(model.DrawingSurfaceList[0], target.ResolvedModel);
                Assert.AreSame(entity1, target.ResolvedEntity);
                Assert.AreEqual(0, writer.CoreMessages.Count);
            }
        }

        /// <summary>
        /// Unit test for the <see cref="SuppressMessage.TryResolve(RuleSet, ThreatModelForge.Model.ThreatModel, MessageWriter)"/> method.
        /// </summary>
        [TestMethod]
        public void TryResolveInvalidTest()
        {
            Entity entity1 = new StencilEllipse()
            {
                Guid = Guid.NewGuid(),
                Properties =
                {
                    new HeaderDisplayAttribute
                    {
                        DisplayName = "Web Service",
                    },
                    new StringDisplayAttribute
                    {
                        Name = "Name",
                        DisplayName = "Name",
                        Value = "Expected",
                    },
                },
            };

            ThreatModel model = new ThreatModel()
            {
                DrawingSurfaceList =
                {
                    new DrawingSurfaceModel
                    {
                        Header = "DFD-0",
                        Borders =
                        {
                            { entity1.Guid, entity1 },
                        },
                    },
                },
            };

            MockMessageWriter writer = new MockMessageWriter();

            using (RuleSet ruleSet = new RuleSet()
            {
                Rules =
                {
                    new Rule1(),
                    new Rule2(),
                },
            })
            {
                SuppressMessage target = new SuppressMessage()
                {
                };

                Assert.IsFalse(target.TryResolve(ruleSet, model, writer));
                Assert.AreEqual(1, writer.CoreMessages.Count);
                writer.CoreMessages.Clear();

                target.RuleID = "9999Invalid";
                Assert.IsFalse(target.TryResolve(ruleSet, model, writer));
                Assert.AreEqual(1, writer.CoreMessages.Count);
                Assert.AreEqual(MessageSeverity.Warning, writer.CoreMessages[0].Item1);
                Assert.IsNotNull(writer.CoreMessages[0].Item2);
                Assert.IsTrue(writer.CoreMessages[0].Item3.Contains(target.RuleID));
                writer.CoreMessages.Clear();

                target.RuleID = ruleSet.Rules[0].ID;
                target.Model = "Invalid-Model";
                Assert.IsFalse(target.TryResolve(ruleSet, model, writer));
                Assert.AreEqual(1, writer.CoreMessages.Count);
                Assert.AreEqual(MessageSeverity.Warning, writer.CoreMessages[0].Item1);
                Assert.IsNotNull(writer.CoreMessages[0].Item2);
                Assert.IsTrue(writer.CoreMessages[0].Item3.Contains(target.Model));
                writer.CoreMessages.Clear();

                target.Model = string.Empty;
                target.Target = "AnythingEntity";
                Assert.IsFalse(target.TryResolve(ruleSet, model, writer));
                Assert.AreEqual(1, writer.CoreMessages.Count);
                Assert.AreEqual(MessageSeverity.Warning, writer.CoreMessages[0].Item1);
                Assert.IsNotNull(writer.CoreMessages[0].Item2);
                Assert.IsTrue(writer.CoreMessages[0].Item3.Contains(target.Target));
                writer.CoreMessages.Clear();

                target.Model = model.DrawingSurfaceList[0].Header;
                target.Target = "InvalidEntity";
                Assert.IsFalse(target.TryResolve(ruleSet, model, writer));
                Assert.AreEqual(1, writer.CoreMessages.Count);
                Assert.AreEqual(MessageSeverity.Warning, writer.CoreMessages[0].Item1);
                Assert.IsNotNull(writer.CoreMessages[0].Item2);
                Assert.IsTrue(writer.CoreMessages[0].Item3.Contains(target.Target));
                writer.CoreMessages.Clear();
            }
        }
    }
}
