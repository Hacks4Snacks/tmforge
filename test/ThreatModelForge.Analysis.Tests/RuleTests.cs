namespace ThreatModelForge.Analysis.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for the <see cref="Rule"/> class.
    /// </summary>
    [TestClass]
    public class RuleTests
    {
        /// <summary>
        /// Unit test for the <see cref="Rule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using (TestRule target = new TestRule())
            {
                Assert.AreEqual("TM1234", target.ID);
            }
        }

        /// <summary>
        /// Unit test for the <see cref="Rule.GetEntityDisplayText(Entity)"/> method.
        /// </summary>
        [TestMethod]
        public void GetEntityDisplayTextTest()
        {
            Entity entity1 = new StencilEllipse();
            string actual = TestRule.AccessGetEntityDisplayText(entity1);
            Assert.AreEqual(
                "Unnamed entity with ID=00000000-0000-0000-0000-000000000000",
                actual);

            StringDisplayAttribute custom1 = new StringDisplayAttribute
            {
                DisplayName = "Custom1",
                Value = "Unexpected",
            };

            StringDisplayAttribute name1 = new StringDisplayAttribute
            {
                Name = "Name",
                DisplayName = "Name",
                Value = "Expected",
            };

            entity1.Properties.Add(custom1);
            entity1.Properties.Add(name1);
            actual = TestRule.AccessGetEntityDisplayText(entity1);
            Assert.AreEqual(
                "Expected of unknown type and ID=00000000-0000-0000-0000-000000000000",
                actual);

            HeaderDisplayAttribute header1 = new HeaderDisplayAttribute
            {
                DisplayName = "Web Service",
            };

            entity1.Properties.Add(header1);
            actual = TestRule.AccessGetEntityDisplayText(entity1);
            Assert.AreEqual(
                "Expected (Web Service) ID=00000000-0000-0000-0000-000000000000",
                actual);

            entity1.Properties.Remove(name1);
            actual = TestRule.AccessGetEntityDisplayText(entity1);
            Assert.AreEqual(
                "Unnamed entity of type Web Service and ID=00000000-0000-0000-0000-000000000000",
                actual);

            try
            {
                Entity? nullEntity = null;
                actual = TestRule.AccessGetEntityDisplayText(nullEntity!);
                Assert.Fail("Expected exception.");
            }
            catch (ArgumentNullException ex)
            {
                Assert.AreEqual("entity", ex.ParamName);
            }
        }

        private class TestRule : Rule
        {
            public TestRule()
                : base(1234, MessageSeverity.Warning, "Test")
            {
            }

            public static string AccessGetEntityDisplayText(Entity entity)
            {
                return GetEntityDisplayText(entity);
            }

            public override void Evaluate(RuleEvaluationContext context)
            {
                throw new NotImplementedException();
            }
        }
    }
}
