namespace ThreatModelForge.Editing.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests guarding the invariants of the built-in <see cref="StencilCatalog"/>.
    /// </summary>
    [TestClass]
    public class StencilCatalogTest
    {
        private static readonly HashSet<string> ValidBases = new HashSet<string>(StringComparer.Ordinal)
        {
            "process",
            "datastore",
            "external",
            "boundary",
        };

        private static readonly HashSet<string> ValidPacks = new HashSet<string>(StringComparer.Ordinal)
        {
            "generic",
            "azure",
            "kubernetes",
            "identity",
            "web",
        };

        /// <summary>
        /// The catalog exposes at least the four generic primitives plus the Azure packs.
        /// </summary>
        [TestMethod]
        public void All_IsNotEmpty()
        {
            Assert.IsTrue(StencilCatalog.All.Count > 0, "The stencil catalog must not be empty.");
        }

        /// <summary>
        /// Every stencil identifier is unique so the palette and drop lookup are unambiguous.
        /// </summary>
        [TestMethod]
        public void All_HaveUniqueIds()
        {
            List<string> ids = StencilCatalog.All.Select(stencil => stencil.Id).ToList();
            CollectionAssert.AllItemsAreUnique(ids, "Stencil ids must be unique.");
        }

        /// <summary>
        /// Every stencil maps to one of the four DFD primitives so the analysis rules apply unchanged.
        /// </summary>
        [TestMethod]
        public void All_MapToAValidPrimitive()
        {
            foreach (StencilDto stencil in StencilCatalog.All)
            {
                Assert.IsTrue(
                    ValidBases.Contains(stencil.Base),
                    $"Stencil '{stencil.Id}' has an invalid base '{stencil.Base}'.");
            }
        }

        /// <summary>
        /// Every stencil carries the display fields the palette needs, and a non-null defaults map.
        /// </summary>
        [TestMethod]
        public void All_HaveRequiredDisplayFields()
        {
            foreach (StencilDto stencil in StencilCatalog.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(stencil.Id), "A stencil is missing its id.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(stencil.Label), $"Stencil '{stencil.Id}' is missing a label.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(stencil.Category), $"Stencil '{stencil.Id}' is missing a category.");
                Assert.IsNotNull(stencil.Defaults, $"Stencil '{stencil.Id}' has null defaults.");
            }
        }

        /// <summary>
        /// The four generic DFD primitives are always offered and map to themselves.
        /// </summary>
        [TestMethod]
        public void All_IncludeTheGenericPrimitives()
        {
            foreach (string primitive in ValidBases)
            {
                StencilDto? stencil = StencilCatalog.All.FirstOrDefault(candidate => candidate.Id == primitive);
                Assert.IsNotNull(stencil, $"The catalog must offer the generic '{primitive}' stencil.");
                Assert.AreEqual(primitive, stencil!.Base, $"The generic '{primitive}' stencil must map to itself.");
            }
        }

        /// <summary>
        /// Every stencil declares a pack, and only the known packs are shipped.
        /// </summary>
        [TestMethod]
        public void All_BelongToAKnownPack()
        {
            foreach (StencilDto stencil in StencilCatalog.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(stencil.Pack), $"Stencil '{stencil.Id}' is missing a pack.");
                Assert.IsTrue(
                    ValidPacks.Contains(stencil.Pack),
                    $"Stencil '{stencil.Id}' has an unknown pack '{stencil.Pack}'.");
            }
        }

        /// <summary>
        /// Every stencil exposes a non-null tag list so the palette search never dereferences null.
        /// </summary>
        [TestMethod]
        public void All_HaveNonNullTags()
        {
            foreach (StencilDto stencil in StencilCatalog.All)
            {
                Assert.IsNotNull(stencil.Tags, $"Stencil '{stencil.Id}' has null tags.");
            }
        }

        /// <summary>
        /// The generic DFD primitives ship in the always-on <c>generic</c> pack.
        /// </summary>
        [TestMethod]
        public void GenericPrimitives_ShipInTheGenericPack()
        {
            foreach (string primitive in ValidBases)
            {
                StencilDto? stencil = StencilCatalog.All.FirstOrDefault(candidate => candidate.Id == primitive);
                Assert.IsNotNull(stencil, $"The catalog must offer the generic '{primitive}' stencil.");
                Assert.AreEqual("generic", stencil!.Pack, $"The generic '{primitive}' stencil must ship in the generic pack.");
            }
        }

        /// <summary>
        /// Every pack is named and reports a positive stencil count.
        /// </summary>
        [TestMethod]
        public void Packs_AreNotEmptyAndNamed()
        {
            Assert.IsTrue(StencilCatalog.Packs.Count > 0, "There must be at least one pack.");
            foreach (PackDto pack in StencilCatalog.Packs)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(pack.Id), "A pack is missing its id.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(pack.Name), $"Pack '{pack.Id}' is missing a name.");
                Assert.IsTrue(pack.Count > 0, $"Pack '{pack.Id}' reports no stencils.");
            }
        }

        /// <summary>
        /// The pack list describes every stencil pack exactly once, with matching counts.
        /// </summary>
        [TestMethod]
        public void Packs_AccountForEveryStencil()
        {
            Dictionary<string, int> byPack = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (StencilDto stencil in StencilCatalog.All)
            {
                byPack[stencil.Pack] = byPack.TryGetValue(stencil.Pack, out int count) ? count + 1 : 1;
            }

            CollectionAssert.AreEquivalent(
                byPack.Keys.ToList(),
                StencilCatalog.Packs.Select(pack => pack.Id).ToList(),
                "Each stencil pack must be described exactly once.");

            foreach (PackDto pack in StencilCatalog.Packs)
            {
                Assert.AreEqual(byPack[pack.Id], pack.Count, $"Pack '{pack.Id}' reports the wrong stencil count.");
            }
        }

        /// <summary>
        /// Each stencil inherits the security-relevant default properties of its DFD primitive.
        /// </summary>
        [TestMethod]
        public void BaseDefaults_AreAppliedByPrimitive()
        {
            foreach (StencilDto stencil in StencilCatalog.All)
            {
                switch (stencil.Base)
                {
                    case "process":
                        Assert.IsTrue(stencil.Defaults.ContainsKey("SanitizesInput"), $"Process '{stencil.Id}' is missing base defaults.");
                        break;
                    case "datastore":
                        Assert.IsTrue(stencil.Defaults.ContainsKey("Encrypted"), $"Data store '{stencil.Id}' is missing base defaults.");
                        break;
                    case "external":
                        Assert.IsTrue(stencil.Defaults.ContainsKey("AuthenticatesItself"), $"External '{stencil.Id}' is missing base defaults.");
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// A stencil's own preset properties override the inherited primitive defaults.
        /// </summary>
        [TestMethod]
        public void StencilDefaults_OverrideBaseDefaults()
        {
            StencilDto blob = StencilCatalog.All.Single(stencil => stencil.Id == "azure-blob");
            Assert.AreEqual("At-rest", blob.Defaults["Encrypted"], "Azure Blob Storage should override the Encrypted default.");

            StencilDto keyVault = StencilCatalog.All.Single(stencil => stencil.Id == "azure-key-vault");
            Assert.AreEqual("Yes", keyVault.Defaults["StoresCredentials"], "Key Vault should store credentials by default.");
        }

        /// <summary>
        /// The Kubernetes pack provides the core control-plane, workload, and secret-store stencils.
        /// </summary>
        [TestMethod]
        public void KubernetesPack_ProvidesClusterModelingStencils()
        {
            string[] expected = { "k8s-cluster", "k8s-namespace", "k8s-api-server", "k8s-etcd", "k8s-secret", "k8s-service-account" };
            foreach (string id in expected)
            {
                Assert.IsTrue(StencilCatalog.All.Any(stencil => stencil.Id == id), $"The catalog must offer the '{id}' stencil.");
            }

            StencilDto etcd = StencilCatalog.All.Single(stencil => stencil.Id == "k8s-etcd");
            Assert.AreEqual("Yes", etcd.Defaults["StoresCredentials"], "etcd stores cluster Secrets.");
        }
    }
}
