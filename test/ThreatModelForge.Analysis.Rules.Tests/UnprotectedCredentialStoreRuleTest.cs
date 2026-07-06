namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for the <see cref="UnprotectedCredentialStoreRule"/> class.
    /// </summary>
    [TestClass]
    public class UnprotectedCredentialStoreRuleTest
    {
        private const string StorageComponentGenericTypeId = "GE.DS";

        /// <summary>
        /// Unit test for the <see cref="UnprotectedCredentialStoreRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using (UnprotectedCredentialStoreRule target = new UnprotectedCredentialStoreRule())
            {
                Assert.AreEqual("TM1020", target.ID);
                Assert.AreEqual(MessageSeverity.Warning, target.Severity);
                Assert.IsNotNull(target.HelpUri);
                Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
                Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
            }
        }

        /// <summary>
        /// A credential store whose access control is None is flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateFlagsCredentialStoreWithoutAccessControlTest()
        {
            StencilParallelLines store = CreateStorageComponent("Secrets DB", ("StoresCredentials", "Yes"), ("AccessControl", "None"));
            ThreatModel model = BuildModel(store);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnprotectedCredentialStoreRule target = new UnprotectedCredentialStoreRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(1, writer.Messages.Count);
            Assert.AreSame(store, writer.Messages[0].Target);
            Assert.IsTrue(writer.Messages[0].Text!.Contains("Secrets DB"));
        }

        /// <summary>
        /// A credential store missing the AccessControl property is flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateFlagsCredentialStoreWithoutAccessControlPropertyTest()
        {
            StencilParallelLines store = CreateStorageComponent("Token Cache", ("StoresCredentials", "Yes"));
            ThreatModel model = BuildModel(store);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnprotectedCredentialStoreRule target = new UnprotectedCredentialStoreRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(1, writer.Messages.Count);
        }

        /// <summary>
        /// A credential store that is publicly reachable is flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateFlagsPublicCredentialStoreTest()
        {
            StencilParallelLines store = CreateStorageComponent("Public Bucket", ("StoresCredentials", "Yes"), ("AccessControl", "Public"));
            ThreatModel model = BuildModel(store);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnprotectedCredentialStoreRule target = new UnprotectedCredentialStoreRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(1, writer.Messages.Count);
        }

        /// <summary>
        /// A credential store protected by RBAC is not flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresRbacCredentialStoreTest()
        {
            StencilParallelLines store = CreateStorageComponent("Key Vault", ("StoresCredentials", "Yes"), ("AccessControl", "RBAC"));
            ThreatModel model = BuildModel(store);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnprotectedCredentialStoreRule target = new UnprotectedCredentialStoreRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// A credential store protected by an ACL is not flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresAclCredentialStoreTest()
        {
            StencilParallelLines store = CreateStorageComponent("File Share", ("StoresCredentials", "Yes"), ("AccessControl", "ACL"));
            ThreatModel model = BuildModel(store);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnprotectedCredentialStoreRule target = new UnprotectedCredentialStoreRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// A store that does not hold credentials is not flagged even without access control.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresNonCredentialStoreTest()
        {
            StencilParallelLines store = CreateStorageComponent("Blob Store", ("StoresCredentials", "No"), ("AccessControl", "None"));
            ThreatModel model = BuildModel(store);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnprotectedCredentialStoreRule target = new UnprotectedCredentialStoreRule())
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
