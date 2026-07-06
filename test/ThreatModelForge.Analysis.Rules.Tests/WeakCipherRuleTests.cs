namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for the <see cref="WeakCipherRule"/> class (TM1025).
    /// </summary>
    [TestClass]
    public class WeakCipherRuleTests
    {
        private const string StorageComponentGenericTypeId = "GE.DS";

        /// <summary>
        /// Verifies the rule's identity and populated metadata.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using WeakCipherRule target = new WeakCipherRule();
            Assert.AreEqual("TM1025", target.ID);
            Assert.AreEqual(MessageSeverity.Warning, target.Severity);
            Assert.IsNotNull(target.HelpUri);
            Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
            Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
        }

        /// <summary>
        /// A store declaring a non-approved cipher (secretbox) is flagged, naming the cipher.
        /// </summary>
        [TestMethod]
        public void FlagsNonApprovedCipher()
        {
            Assert.AreEqual(1, Evaluate("KEK Store", ("Algorithm", "secretbox"), out Message? message));
            Assert.IsNotNull(message);
            Assert.IsTrue(message!.Text!.Contains("secretbox"));
        }

        /// <summary>
        /// Unauthenticated AES-CBC (without a MAC) is flagged.
        /// </summary>
        [TestMethod]
        public void FlagsUnauthenticatedAesCbc()
        {
            Assert.AreEqual(1, Evaluate("Cache", ("Algorithm", "AES-CBC"), out _));
        }

        /// <summary>
        /// The Cipher property is honored as an alias for Algorithm.
        /// </summary>
        [TestMethod]
        public void FlagsViaCipherAlias()
        {
            Assert.AreEqual(1, Evaluate("Legacy DB", ("Cipher", "3DES"), out _));
        }

        /// <summary>
        /// Approved authenticated ciphers are not flagged.
        /// </summary>
        [TestMethod]
        public void IgnoresApprovedCiphers()
        {
            Assert.AreEqual(0, Evaluate("A", ("Algorithm", "AES-GCM"), out _));
            Assert.AreEqual(0, Evaluate("B", ("Algorithm", "AES-256-GCM"), out _));
            Assert.AreEqual(0, Evaluate("C", ("Algorithm", "ChaCha20-Poly1305"), out _));
            Assert.AreEqual(0, Evaluate("D", ("Algorithm", "AES-CBC+HMAC-SHA256"), out _));
        }

        /// <summary>
        /// A store with no cipher (absent or None) is not flagged.
        /// </summary>
        [TestMethod]
        public void IgnoresUndeclaredCipher()
        {
            Assert.AreEqual(0, Evaluate("NoAlgo", System.Array.Empty<(string, string)>(), out _));
            Assert.AreEqual(0, Evaluate("NoneAlgo", ("Algorithm", "None"), out _));
        }

        private static int Evaluate(string name, (string Name, string Value) property, out Message? message)
        {
            return Evaluate(name, new[] { property }, out message);
        }

        private static int Evaluate(string name, (string Name, string Value)[] properties, out Message? message)
        {
            StencilParallelLines store = CreateStorageComponent(name, properties);
            ThreatModel model = BuildModel(store);
            MockMessageWriter writer = new MockMessageWriter();

            using (WeakCipherRule target = new WeakCipherRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            message = writer.Messages.Count > 0 ? writer.Messages[0] : null;
            return writer.Messages.Count;
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
