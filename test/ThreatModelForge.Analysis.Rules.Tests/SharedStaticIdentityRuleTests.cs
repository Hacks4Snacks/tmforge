namespace ThreatModelForge.Analysis.Rules.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the <see cref="SharedStaticIdentityRule"/> class (TM1026).
    /// </summary>
    [TestClass]
    public class SharedStaticIdentityRuleTests
    {
        /// <summary>
        /// Verifies the rule's identity and populated metadata.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using SharedStaticIdentityRule target = new SharedStaticIdentityRule();
            Assert.AreEqual("TM1026", target.ID);
            Assert.AreEqual(MessageSeverity.Warning, target.Severity);
            Assert.IsNotNull(target.HelpUri);
            Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
            Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
        }

        /// <summary>
        /// One identity asserted by flows from two distinct sources flags both flows.
        /// </summary>
        [TestMethod]
        public void FlagsIdentitySharedAcrossDistinctSources()
        {
            ThreatModel model = BuildModel(
                Edge(Guid.NewGuid(), "cred-manager-sa"),
                Edge(Guid.NewGuid(), "cred-manager-sa"));

            MockMessageWriter writer = Evaluate(model);

            Assert.AreEqual(2, writer.Messages.Count);
            Assert.IsTrue(writer.Messages[0].Text!.Contains("cred-manager-sa"));
        }

        /// <summary>
        /// Distinct identities on distinct sources are fine.
        /// </summary>
        [TestMethod]
        public void IgnoresDistinctIdentities()
        {
            ThreatModel model = BuildModel(
                Edge(Guid.NewGuid(), "sa-one"),
                Edge(Guid.NewGuid(), "sa-two"));

            Assert.AreEqual(0, Evaluate(model).Messages.Count);
        }

        /// <summary>
        /// A single source reusing its own identity across flows is not shared.
        /// </summary>
        [TestMethod]
        public void IgnoresSameSourceReusingIdentity()
        {
            Guid source = Guid.NewGuid();
            ThreatModel model = BuildModel(
                Edge(source, "sa-one"),
                Edge(source, "sa-one"));

            Assert.AreEqual(0, Evaluate(model).Messages.Count);
        }

        /// <summary>
        /// Flows without a declared identity are ignored.
        /// </summary>
        [TestMethod]
        public void IgnoresUndeclaredIdentity()
        {
            ThreatModel model = BuildModel(
                Edge(Guid.NewGuid(), null),
                Edge(Guid.NewGuid(), null));

            Assert.AreEqual(0, Evaluate(model).Messages.Count);
        }

        private static MockMessageWriter Evaluate(ThreatModel model)
        {
            MockMessageWriter writer = new MockMessageWriter();
            using (SharedStaticIdentityRule target = new SharedStaticIdentityRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            return writer;
        }

        private static Connector Edge(Guid source, string? identity)
        {
            Connector edge = new Connector { Guid = Guid.NewGuid(), SourceGuid = source, TargetGuid = Guid.NewGuid() };
            if (identity != null)
            {
                edge.Properties.Add(new CustomStringDisplayAttribute { Value = "Identity:" + identity });
            }

            return edge;
        }

        private static ThreatModel BuildModel(params Connector[] edges)
        {
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Header = "DFD-0" };
            foreach (Connector edge in edges)
            {
                diagram.Lines.Add(edge.Guid, edge);
            }

            return new ThreatModel { DrawingSurfaceList = { diagram } };
        }
    }
}
