namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for the <see cref="CredentialsInLogStoreRule"/> class.
    /// </summary>
    [TestClass]
    public class CredentialsInLogStoreRuleTest
    {
        private const string StorageComponentGenericTypeId = "GE.DS";

        /// <summary>
        /// Unit test for the <see cref="CredentialsInLogStoreRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using (CredentialsInLogStoreRule target = new CredentialsInLogStoreRule())
            {
                Assert.AreEqual("TM1022", target.ID);
                Assert.AreEqual(MessageSeverity.Warning, target.Severity);
                Assert.IsNotNull(target.HelpUri);
                Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
                Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
            }
        }

        /// <summary>
        /// A store that records log data and also stores credentials is flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateFlagsLogStoreHoldingCredentialsTest()
        {
            StencilParallelLines store = CreateStorageComponent("App Log", ("StoresLogData", "Yes"), ("StoresCredentials", "Yes"));
            ThreatModel model = BuildModel(store);
            MockMessageWriter writer = new MockMessageWriter();

            using (CredentialsInLogStoreRule target = new CredentialsInLogStoreRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(1, writer.Messages.Count);
            Assert.AreSame(store, writer.Messages[0].Target);
            Assert.IsTrue(writer.Messages[0].Text!.Contains("App Log"));
        }

        /// <summary>
        /// A log store that does not hold credentials is not flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresLogOnlyStoreTest()
        {
            StencilParallelLines store = CreateStorageComponent("Audit Log", ("StoresLogData", "Yes"), ("StoresCredentials", "No"));
            ThreatModel model = BuildModel(store);
            MockMessageWriter writer = new MockMessageWriter();

            using (CredentialsInLogStoreRule target = new CredentialsInLogStoreRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// A credential store that does not record log data is not flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresCredentialOnlyStoreTest()
        {
            StencilParallelLines store = CreateStorageComponent("Key Vault", ("StoresLogData", "No"), ("StoresCredentials", "Yes"));
            ThreatModel model = BuildModel(store);
            MockMessageWriter writer = new MockMessageWriter();

            using (CredentialsInLogStoreRule target = new CredentialsInLogStoreRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        private static StencilParallelLines CreateStorageComponent(string name, params (string Name, string Value)[] properties)
        {
            StencilParallelLines store = new StencilParallelLines
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = StorageComponentGenericTypeId,
            };
            store.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = name });
            foreach ((string propertyName, string propertyValue) in properties)
            {
                store.Properties.Add(new CustomStringDisplayAttribute { Value = $"{propertyName}:{propertyValue}" });
            }

            return store;
        }

        private static ThreatModel BuildModel(Entity component)
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
                            { component.Guid, component },
                        },
                    },
                },
            };
        }
    }
}
