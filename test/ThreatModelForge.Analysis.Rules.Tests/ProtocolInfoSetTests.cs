namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for <see cref="ProtocolInfoSet"/> (the well-known protocol table and the
    /// <c>PROTOCOLS</c> merge behavior).
    /// </summary>
    [TestClass]
    public class ProtocolInfoSetTests
    {
        /// <summary>
        /// The default set includes the modern protocols the authoring schema recommends, each with
        /// a port so the missing-port rule (TM1010) can infer one.
        /// </summary>
        [TestMethod]
        public void DefaultIncludesModernProtocols()
        {
            IReadOnlyDictionary<string, ProtocolInfo> protocols = ProtocolInfoSet.Default.Protocols;
            foreach (string name in new[] { "TLS", "mTLS", "gRPC", "AMQP", "SQL" })
            {
                Assert.IsTrue(protocols.TryGetValue(name, out ProtocolInfo? info), $"the default protocol set should include {name}");
                Assert.IsTrue(info!.DefaultPort > 0, $"{name} should infer a default port");
            }
        }

        /// <summary>
        /// A supplied <c>PROTOCOLS</c> variable augments (and overrides by name) the defaults rather
        /// than replacing them.
        /// </summary>
        [TestMethod]
        public void FromContextMergesWithDefaults()
        {
            ThreatModel model = new ();
            MockMessageWriter writer = new ();
            RuleEvaluationContext context = new (model, writer, new Dictionary<string, string> { ["PROTOCOLS"] = "CoAP:5683" });

            ProtocolInfoSet set = ProtocolInfoSet.FromContext(context);

            Assert.IsTrue(set.Protocols.TryGetValue("CoAP", out ProtocolInfo? coap), "the custom protocol should be added");
            Assert.AreEqual(5683, coap!.DefaultPort);
            Assert.IsTrue(set.Protocols.ContainsKey("HTTPS"), "built-in defaults should be retained after the merge");
            Assert.IsTrue(set.Protocols.ContainsKey("gRPC"), "built-in defaults should be retained after the merge");
        }
    }
}
