namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for the <see cref="DataStoreMissingBackupRule"/> class (TM1028).
    /// </summary>
    [TestClass]
    public class DataStoreMissingBackupRuleTests
    {
        private const string StorageComponentGenericTypeId = "GE.DS";

        /// <summary>
        /// Verifies the rule's identity and populated metadata.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using DataStoreMissingBackupRule target = new DataStoreMissingBackupRule();
            Assert.AreEqual("TM1028", target.ID);
            Assert.AreEqual(MessageSeverity.Warning, target.Severity);
            Assert.IsNotNull(target.HelpUri);
            Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
            Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
        }

        /// <summary>
        /// A credential store with Backup = No is flagged.
        /// </summary>
        [TestMethod]
        public void FlagsCredentialStoreWithoutBackup()
        {
            StencilParallelLines store = CreateStore("Vault", ("StoresCredentials", "Yes"), ("Backup", "No"));
            MockMessageWriter writer = Evaluate(store);

            Assert.AreEqual(1, writer.Messages.Count);
            Assert.AreSame(store, writer.Messages[0].Target);
            Assert.IsTrue(writer.Messages[0].Text!.Contains("Vault"));
        }

        /// <summary>
        /// A log store that never declares Backup is flagged (unset is treated as no backup).
        /// </summary>
        [TestMethod]
        public void FlagsLogStoreWithoutBackupProperty()
        {
            StencilParallelLines store = CreateStore("Audit Log", ("StoresLogData", "Yes"));
            MockMessageWriter writer = Evaluate(store);

            Assert.AreEqual(1, writer.Messages.Count);
        }

        /// <summary>
        /// A credential store that is backed up is not flagged.
        /// </summary>
        [TestMethod]
        public void IgnoresBackedUpCredentialStore()
        {
            StencilParallelLines store = CreateStore("Vault", ("StoresCredentials", "Yes"), ("Backup", "Yes"));
            MockMessageWriter writer = Evaluate(store);

            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// A store that holds no important data is not flagged even without a backup.
        /// </summary>
        [TestMethod]
        public void IgnoresUnimportantStoreWithoutBackup()
        {
            StencilParallelLines store = CreateStore("Scratch Cache", ("Backup", "No"));
            MockMessageWriter writer = Evaluate(store);

            Assert.AreEqual(0, writer.Messages.Count);
        }

        private static MockMessageWriter Evaluate(Entity store)
        {
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Header = "DFD-0" };
            diagram.Borders.Add(store.Guid, store);
            ThreatModel model = new ThreatModel { DrawingSurfaceList = { diagram } };

            MockMessageWriter writer = new MockMessageWriter();
            using (DataStoreMissingBackupRule target = new DataStoreMissingBackupRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            return writer;
        }

        private static StencilParallelLines CreateStore(string name, params (string Name, string Value)[] properties)
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
    }
}
