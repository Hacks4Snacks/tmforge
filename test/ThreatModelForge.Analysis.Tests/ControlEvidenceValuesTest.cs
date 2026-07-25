namespace ThreatModelForge.Analysis.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for the <see cref="ControlEvidenceValues"/> class.
    /// </summary>
    [TestClass]
    public class ControlEvidenceValuesTest
    {
        /// <summary>
        /// A missing, blank, or Unknown value records no evidence either way.
        /// </summary>
        /// <param name="value">The raw property value under test.</param>
        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("\t")]
        [DataRow("Unknown")]
        [DataRow("unknown")]
        [DataRow("UNKNOWN")]
        [DataRow("  Unknown  ")]
        public void IsUnevidencedRecognisesUnevidencedValuesTest(string? value)
        {
            Assert.IsTrue(ControlEvidenceValues.IsUnevidenced(value));
        }

        /// <summary>
        /// Any value an author actually recorded is evidence, including negative ones.
        /// </summary>
        /// <param name="value">The raw property value under test.</param>
        [TestMethod]
        [DataRow("No")]
        [DataRow("None")]
        [DataRow("Yes")]
        [DataRow("TDE")]
        [DataRow("Unknowns")]
        [DataRow("Not Unknown")]
        public void IsUnevidencedRejectsEvidencedValuesTest(string value)
        {
            Assert.IsFalse(ControlEvidenceValues.IsUnevidenced(value));
        }

        /// <summary>
        /// With a denylist vocabulary, an unevidenced value must not be read as the control being present.
        /// </summary>
        /// <param name="value">The raw property value under test.</param>
        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("Unknown")]
        public void ClassifyByAbsentValuesTreatsUnevidencedAsUnevidencedTest(string? value)
        {
            Assert.AreEqual(ControlEvidence.Unevidenced, ControlEvidenceValues.ClassifyByAbsentValues(value, "No"));
        }

        /// <summary>
        /// A listed negative value is a stated absence, matched without regard to case or padding.
        /// </summary>
        /// <param name="value">The raw property value under test.</param>
        [TestMethod]
        [DataRow("No")]
        [DataRow("no")]
        [DataRow("  No  ")]
        public void ClassifyByAbsentValuesRecognisesStatedAbsenceTest(string value)
        {
            Assert.AreEqual(ControlEvidence.Absent, ControlEvidenceValues.ClassifyByAbsentValues(value, "No"));
        }

        /// <summary>
        /// Any other evidenced value counts as the control being present, so the open vocabulary keeps
        /// working when a model names a mechanism the rule has never heard of.
        /// </summary>
        /// <param name="value">The raw property value under test.</param>
        [TestMethod]
        [DataRow("Yes")]
        [DataRow("TDE")]
        [DataRow("Client-side")]
        [DataRow("Some future mechanism")]
        public void ClassifyByAbsentValuesTreatsOtherValuesAsPresentTest(string value)
        {
            Assert.AreEqual(ControlEvidence.Present, ControlEvidenceValues.ClassifyByAbsentValues(value, "No"));
        }

        /// <summary>
        /// With an allow list vocabulary, an unevidenced value must not be read as the control being present.
        /// </summary>
        /// <param name="value">The raw property value under test.</param>
        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("Unknown")]
        public void ClassifyByPresentValuesTreatsUnevidencedAsUnevidencedTest(string? value)
        {
            Assert.AreEqual(ControlEvidence.Unevidenced, ControlEvidenceValues.ClassifyByPresentValues(value, "RBAC", "ACL"));
        }

        /// <summary>
        /// A listed positive value is the control being present, matched without regard to case or padding.
        /// </summary>
        /// <param name="value">The raw property value under test.</param>
        [TestMethod]
        [DataRow("RBAC")]
        [DataRow("rbac")]
        [DataRow("  ACL  ")]
        public void ClassifyByPresentValuesRecognisesStatedPresenceTest(string value)
        {
            Assert.AreEqual(ControlEvidence.Present, ControlEvidenceValues.ClassifyByPresentValues(value, "RBAC", "ACL"));
        }

        /// <summary>
        /// Any other evidenced value is a stated absence, because only the listed values protect anything.
        /// </summary>
        /// <param name="value">The raw property value under test.</param>
        [TestMethod]
        [DataRow("None")]
        [DataRow("Public")]
        [DataRow("Something else")]
        public void ClassifyByPresentValuesTreatsOtherValuesAsAbsentTest(string value)
        {
            Assert.AreEqual(ControlEvidence.Absent, ControlEvidenceValues.ClassifyByPresentValues(value, "RBAC", "ACL"));
        }
    }
}
