namespace ThreatModelForge.Analysis.Rules.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the <see cref="CachedCredentialReadRule"/> class (TM1027).
    /// </summary>
    [TestClass]
    public class CachedCredentialReadRuleTests
    {
        private const string StorageComponentGenericTypeId = "GE.DS";

        /// <summary>
        /// Verifies the rule's identity and populated metadata.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using CachedCredentialReadRule target = new CachedCredentialReadRule();
            Assert.AreEqual("TM1027", target.ID);
            Assert.AreEqual(MessageSeverity.Warning, target.Severity);
            Assert.IsNotNull(target.HelpUri);
            Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
            Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
        }

        /// <summary>
        /// A cached read from a credential store is flagged.
        /// </summary>
        [TestMethod]
        public void FlagsCachedReadFromCredentialStore()
        {
            StencilParallelLines store = CreateStore("KEK", ("StoresCredentials", "Yes"));
            Connector edge = Edge(store.Guid, ("Cached", "Yes"));
            Assert.AreEqual(1, Evaluate(store, edge).Messages.Count);
        }

        /// <summary>
        /// A fresh (non-cached) read from a credential store is not flagged.
        /// </summary>
        [TestMethod]
        public void IgnoresFreshReadFromCredentialStore()
        {
            StencilParallelLines store = CreateStore("KEK", ("StoresCredentials", "Yes"));
            Connector edge = Edge(store.Guid, ("Cached", "No"));
            Assert.AreEqual(0, Evaluate(store, edge).Messages.Count);
        }

        /// <summary>
        /// A cached read from a store that does not hold credentials is not flagged.
        /// </summary>
        [TestMethod]
        public void IgnoresCachedReadFromNonCredentialStore()
        {
            StencilParallelLines store = CreateStore("Cache");
            Connector edge = Edge(store.Guid, ("Cached", "Yes"));
            Assert.AreEqual(0, Evaluate(store, edge).Messages.Count);
        }

        private static MockMessageWriter Evaluate(StencilParallelLines store, Connector edge)
        {
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Header = "DFD-0" };
            diagram.Borders.Add(store.Guid, store);
            diagram.Lines.Add(edge.Guid, edge);
            ThreatModel model = new ThreatModel { DrawingSurfaceList = { diagram } };

            MockMessageWriter writer = new MockMessageWriter();
            using (CachedCredentialReadRule target = new CachedCredentialReadRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            return writer;
        }

        private static StencilParallelLines CreateStore(string name, params (string Name, string Value)[] properties)
        {
            StencilParallelLines store = new StencilParallelLines { Guid = Guid.NewGuid(), GenericTypeId = StorageComponentGenericTypeId };
            store.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = name });
            foreach ((string propertyName, string propertyValue) in properties)
            {
                store.Properties.Add(new CustomStringDisplayAttribute { Value = $"{propertyName}:{propertyValue}" });
            }

            return store;
        }

        private static Connector Edge(Guid source, params (string Name, string Value)[] properties)
        {
            Connector edge = new Connector { Guid = Guid.NewGuid(), SourceGuid = source, TargetGuid = Guid.NewGuid() };
            foreach ((string propertyName, string propertyValue) in properties)
            {
                edge.Properties.Add(new CustomStringDisplayAttribute { Value = $"{propertyName}:{propertyValue}" });
            }

            return edge;
        }
    }
}
