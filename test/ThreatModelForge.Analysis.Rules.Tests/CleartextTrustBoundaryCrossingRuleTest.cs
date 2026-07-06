namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the <see cref="CleartextTrustBoundaryCrossingRule"/> class.
    /// </summary>
    [TestClass]
    public class CleartextTrustBoundaryCrossingRuleTest
    {
        /// <summary>
        /// Unit test for the <see cref="CleartextTrustBoundaryCrossingRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using (CleartextTrustBoundaryCrossingRule target = new CleartextTrustBoundaryCrossingRule())
            {
                Assert.AreEqual("TM1016", target.ID);
                Assert.AreEqual(MessageSeverity.Warning, target.Severity);
                Assert.IsNotNull(target.HelpUri);
                Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
                Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
            }
        }

        /// <summary>
        /// An edge that crosses a trust boundary using a cleartext protocol is flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateFlagsCleartextCrossingTest()
        {
            BorderBoundary border = CreateBoundary();
            Connector crossing = CreateCrossingConnector("Fetch orders", "HTTP");
            ThreatModel model = BuildModel(border, crossing);
            MockMessageWriter writer = new MockMessageWriter();

            using (CleartextTrustBoundaryCrossingRule target = new CleartextTrustBoundaryCrossingRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(1, writer.Messages.Count);
            Message actual = writer.Messages[0];
            Assert.AreEqual(MessageSeverity.Warning, actual.Severity);
            Assert.AreSame(crossing, actual.Target);
            Assert.IsNotNull(actual.Text);
            Assert.IsTrue(actual.Text!.Contains("HTTP"));
            Assert.IsTrue(actual.Text!.Contains("Fetch orders"));
        }

        /// <summary>
        /// An edge that crosses a trust boundary using an encrypted protocol is not flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresEncryptedCrossingTest()
        {
            BorderBoundary border = CreateBoundary();
            Connector crossing = CreateCrossingConnector("Fetch orders", "HTTPS");
            ThreatModel model = BuildModel(border, crossing);
            MockMessageWriter writer = new MockMessageWriter();

            using (CleartextTrustBoundaryCrossingRule target = new CleartextTrustBoundaryCrossingRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// A cleartext edge that does not cross a trust boundary is not flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresCleartextEdgeThatDoesNotCrossBoundaryTest()
        {
            BorderBoundary border = CreateBoundary();
            Connector inside = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceX = 10,
                SourceY = 11,
                TargetX = 12,
                TargetY = 15,
            };
            inside.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = "Local call" });
            inside.Properties.Add(new CustomStringDisplayAttribute { Value = "Protocol:HTTP" });
            ThreatModel model = BuildModel(border, inside);
            MockMessageWriter writer = new MockMessageWriter();

            using (CleartextTrustBoundaryCrossingRule target = new CleartextTrustBoundaryCrossingRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// An edge that crosses a trust boundary but has no declared protocol is not flagged by this rule.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresCrossingWithoutProtocolTest()
        {
            BorderBoundary border = CreateBoundary();
            Connector crossing = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceX = 0,
                SourceY = 0,
                TargetX = 10,
                TargetY = 11,
            };
            crossing.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = "Fetch orders" });
            ThreatModel model = BuildModel(border, crossing);
            MockMessageWriter writer = new MockMessageWriter();

            using (CleartextTrustBoundaryCrossingRule target = new CleartextTrustBoundaryCrossingRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        private static Connector CreateCrossingConnector(string name, string protocol)
        {
            Connector crossing = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceX = 0,
                SourceY = 0,
                TargetX = 10,
                TargetY = 11,
            };
            crossing.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = name });
            crossing.Properties.Add(new CustomStringDisplayAttribute { Value = $"Protocol:{protocol}" });
            return crossing;
        }

        private static BorderBoundary CreateBoundary()
        {
            return new BorderBoundary
            {
                Guid = Guid.NewGuid(),
                Left = 5,
                Top = 10,
                Height = 15,
                Width = 20,
            };
        }

        private static ThreatModel BuildModel(BorderBoundary border, Connector connector)
        {
            return new ThreatModel
            {
                DrawingSurfaceList =
                {
                    new DrawingSurfaceModel
                    {
                        Header = "DFD-0",
                        Borders =
                        {
                            { border.Guid, border },
                        },
                        Lines =
                        {
                            { connector.Guid, connector },
                        },
                    },
                },
            };
        }
    }
}
