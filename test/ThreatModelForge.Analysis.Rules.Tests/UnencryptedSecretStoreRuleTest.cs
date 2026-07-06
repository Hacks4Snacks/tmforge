namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for the <see cref="UnencryptedSecretStoreRule"/> class.
    /// </summary>
    [TestClass]
    public class UnencryptedSecretStoreRuleTest
    {
        private const string StorageComponentGenericTypeId = "GE.DS";

        /// <summary>
        /// Unit test for the <see cref="UnencryptedSecretStoreRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using (UnencryptedSecretStoreRule target = new UnencryptedSecretStoreRule())
            {
                Assert.AreEqual("TM1014", target.ID);
                Assert.AreEqual(MessageSeverity.Warning, target.Severity);
                Assert.IsNotNull(target.HelpUri);
                Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
                Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
            }
        }

        /// <summary>
        /// A data store that stores credentials but is not encrypted at rest is flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateFlagsUnencryptedCredentialStoreTest()
        {
            StencilParallelLines store = CreateStorageComponent("Secrets DB", ("StoresCredentials", "Yes"), ("Encrypted", "No"));
            ThreatModel model = BuildModel(store);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnencryptedSecretStoreRule target = new UnencryptedSecretStoreRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(1, writer.Messages.Count);
            Message actual = writer.Messages[0];
            Assert.AreEqual(MessageSeverity.Warning, actual.Severity);
            Assert.AreSame(store, actual.Target);
            Assert.IsNotNull(actual.Text);
            Assert.IsTrue(actual.Text!.Contains("Secrets DB"));
        }

        /// <summary>
        /// A data store that stores credentials but is missing the Encrypted property is flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateFlagsCredentialStoreWithoutEncryptedPropertyTest()
        {
            StencilParallelLines store = CreateStorageComponent("Token Cache", ("StoresCredentials", "Yes"));
            ThreatModel model = BuildModel(store);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnencryptedSecretStoreRule target = new UnencryptedSecretStoreRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(1, writer.Messages.Count);
        }

        /// <summary>
        /// A data store that stores credentials and is encrypted at rest is not flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresEncryptedCredentialStoreTest()
        {
            StencilParallelLines store = CreateStorageComponent("Key Vault", ("StoresCredentials", "Yes"), ("Encrypted", "Yes"));
            ThreatModel model = BuildModel(store);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnencryptedSecretStoreRule target = new UnencryptedSecretStoreRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// A data store that has some other form of at-rest encryption (for example TDE) is not flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresStoreWithAlternateEncryptionValueTest()
        {
            StencilParallelLines store = CreateStorageComponent("SQL DB", ("StoresCredentials", "Yes"), ("Encrypted", "TDE"));
            ThreatModel model = BuildModel(store);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnencryptedSecretStoreRule target = new UnencryptedSecretStoreRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// A data store that does not store credentials is not flagged even when unencrypted.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresNonCredentialStoreTest()
        {
            StencilParallelLines store = CreateStorageComponent("Blob Store", ("StoresCredentials", "No"), ("Encrypted", "No"));
            ThreatModel model = BuildModel(store);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnencryptedSecretStoreRule target = new UnencryptedSecretStoreRule())
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
