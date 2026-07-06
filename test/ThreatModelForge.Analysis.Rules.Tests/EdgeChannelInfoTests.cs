namespace ThreatModelForge.Analysis.Rules.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for <see cref="EdgeChannelInfo"/> and the non-network-edge skip in the edge rules.
    /// </summary>
    [TestClass]
    public class EdgeChannelInfoTests
    {
        /// <summary>
        /// An edge with no declared channel is treated as a network edge.
        /// </summary>
        [TestMethod]
        public void UndeclaredChannelIsNetwork()
        {
            Connector edge = new () { Guid = Guid.NewGuid() };
            Assert.IsTrue(EdgeChannelInfo.IsNetworkChannel(edge));
        }

        /// <summary>
        /// Non-network channel values are not treated as network edges.
        /// </summary>
        [TestMethod]
        public void NonNetworkChannelsAreNotNetwork()
        {
            foreach (string channel in new[] { "In-Process", "Local-file", "Unix-socket", "Unix", "Loopback", "IPC" })
            {
                Assert.IsFalse(EdgeChannelInfo.IsNetworkChannel(Edge(channel)), $"{channel} should not be a network channel");
            }
        }

        /// <summary>
        /// The explicit Network channel is a network edge.
        /// </summary>
        [TestMethod]
        public void NetworkChannelIsNetwork()
        {
            Assert.IsTrue(EdgeChannelInfo.IsNetworkChannel(Edge("Network")));
        }

        /// <summary>
        /// A non-network edge lacking protocol/port does not raise the missing-port finding (TM1010).
        /// </summary>
        [TestMethod]
        public void NonNetworkEdgeSkipsMissingPortRule()
        {
            Connector edge = Edge("In-Process");
            edge.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = "local KEK read" });
            edge.Properties.Add(new HeaderDisplayAttribute { DisplayName = "Flow", Value = "Flow" });

            ThreatModel model = new ()
            {
                DrawingSurfaceList = { new DrawingSurfaceModel { Lines = { { edge.Guid, edge } } } },
            };

            using var target = new EdgeMissingPortRule();
            MockMessageWriter writer = new ();
            target.Evaluate(new RuleEvaluationContext(model, writer));

            Assert.AreEqual(0, writer.Messages.Count, "a non-network edge must not raise the missing-port finding");
        }

        private static Connector Edge(string channel)
        {
            Connector edge = new () { Guid = Guid.NewGuid() };
            edge.Properties.Add(new CustomStringDisplayAttribute { Value = "Channel:" + channel });
            return edge;
        }
    }
}
