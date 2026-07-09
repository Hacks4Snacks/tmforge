namespace ThreatModelForge.Analysis.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the <see cref="SuppressedMessageWriter"/> class.
    /// </summary>
    [TestClass]
    public class SuppressedMessageWriterTests
    {
        /// <summary>
        /// Unit test for the <see cref="SuppressedMessageWriter"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            SuppressedMessageWriter target = new SuppressedMessageWriter(
                new MockMessageWriter());
            Assert.IsNotNull(target.Inner);

            try
            {
                MessageWriter? nullMessageWriter = null;
                _ = new SuppressedMessageWriter(nullMessageWriter!);
                Assert.Fail("Expected exception.");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("inner", ex.ParamName);
            }
        }

        /// <summary>
        /// Unit test for the <see cref="SuppressedMessageWriter.Write(Message)"/> method.
        /// </summary>
        [TestMethod]
        public void WriteWithSuppressionTest()
        {
            MockMessageWriter inner = new MockMessageWriter();
            SuppressedMessageWriter target = new SuppressedMessageWriter(inner);
            Rule rule = new Rule1();
            StencilEllipse entity1 = new StencilEllipse
            {
                Guid = Guid.NewGuid(),
            };

            using (RuleSet ruleSet = new RuleSet()
            {
                Rules =
                {
                    rule,
                },
            })
            {
                ThreatModel model = new ThreatModel
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

                SuppressMessage suppression = new SuppressMessage()
                {
                    Justification = "Test.",
                    RuleID = rule.ID,
                    Model = "DFD-0",
                    Target = entity1.Guid.ToString(),
                };

                Assert.IsTrue(suppression.TryResolve(ruleSet, model, target));
                target.Suppressions.Add(suppression);

                Message message = new Message
                {
                    Model = model.DrawingSurfaceList[0],
                    Severity = MessageSeverity.Error,
                    Source = rule,
                    Target = entity1,
                    Text = "Should be suppressed.",
                };

                // test the message is suppressed.
                target.Write(message);
                Assert.AreEqual(0, inner.Messages.Count);

                // test another message is not suppressed.
                message = new Message
                {
                    Model = model.DrawingSurfaceList[0],
                    Severity = MessageSeverity.Error,
                    Source = new Rule2(),
                    Target = entity1,
                    Text = "Should not be suppressed.",
                };

                target.Write(message);
                Assert.AreEqual(1, inner.Messages.Count);
            }
        }
    }
}
