namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for the <see cref="UnsignedAuditLogStoreRule"/> class.
    /// </summary>
    [TestClass]
    public class UnsignedAuditLogStoreRuleTest
    {
        private const string StorageComponentGenericTypeId = "GE.DS";

        /// <summary>
        /// Unit test for the <see cref="UnsignedAuditLogStoreRule"/> constructor.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using (UnsignedAuditLogStoreRule target = new UnsignedAuditLogStoreRule())
            {
                Assert.AreEqual("TM1021", target.ID);
                Assert.AreEqual(MessageSeverity.Warning, target.Severity);
                Assert.IsNotNull(target.HelpUri);
                Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
                Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
            }
        }

        /// <summary>
        /// A log store that is not signed is flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateFlagsUnsignedLogStoreTest()
        {
            StencilParallelLines store = CreateStorageComponent("Audit Log", ("StoresLogData", "Yes"), ("Signed", "No"));
            ThreatModel model = BuildModel(store);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnsignedAuditLogStoreRule target = new UnsignedAuditLogStoreRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(1, writer.Messages.Count);
            Assert.AreSame(store, writer.Messages[0].Target);
            Assert.IsTrue(writer.Messages[0].Text!.Contains("Audit Log"));
        }

        /// <summary>
        /// A log store missing the Signed property is flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateFlagsLogStoreWithoutSignedPropertyTest()
        {
            StencilParallelLines store = CreateStorageComponent("Event Log", ("StoresLogData", "Yes"));
            ThreatModel model = BuildModel(store);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnsignedAuditLogStoreRule target = new UnsignedAuditLogStoreRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(1, writer.Messages.Count);
        }

        /// <summary>
        /// A signed log store is not flagged.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresSignedLogStoreTest()
        {
            StencilParallelLines store = CreateStorageComponent("Sealed Ledger", ("StoresLogData", "Yes"), ("Signed", "Yes"));
            ThreatModel model = BuildModel(store);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnsignedAuditLogStoreRule target = new UnsignedAuditLogStoreRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// A store that does not hold log data is not flagged even when unsigned.
        /// </summary>
        [TestMethod]
        public void EvaluateIgnoresNonLogStoreTest()
        {
            StencilParallelLines store = CreateStorageComponent("Blob Store", ("StoresLogData", "No"), ("Signed", "No"));
            ThreatModel model = BuildModel(store);
            MockMessageWriter writer = new MockMessageWriter();

            using (UnsignedAuditLogStoreRule target = new UnsignedAuditLogStoreRule())
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
