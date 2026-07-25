namespace ThreatModelForge.Analysis.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for the <see cref="FindingIdentity"/> class.
    /// </summary>
    [TestClass]
    public class FindingIdentityTests
    {
        /// <summary>
        /// An identity names the rule, the diagram, the target, and the occurrence, in that order.
        /// </summary>
        [TestMethod]
        public void FormatNamesEverySegmentTest()
        {
            Assert.AreEqual("TM1014:page-1:store-1:0", FindingIdentity.Format("TM1014", "page-1", "store-1", 0));
        }

        /// <summary>
        /// A finding about the model as a whole, or about a diagram rather than an element, still gets
        /// a complete identity: the absent segments read as the model scope rather than as blanks.
        /// </summary>
        /// <param name="diagram">The diagram key under test.</param>
        /// <param name="target">The target key under test.</param>
        /// <param name="expected">The expected identity.</param>
        [TestMethod]
        [DataRow(null, null, "TM1003:model:model:0")]
        [DataRow("page-1", null, "TM1003:page-1:model:0")]
        [DataRow("", "  ", "TM1003:model:model:0")]
        public void FormatFallsBackToTheModelScopeTest(string? diagram, string? target, string expected)
        {
            Assert.AreEqual(expected, FindingIdentity.Format("TM1003", diagram, target, 0));
        }

        /// <summary>
        /// The occurrence counter advances per scope, so a rule that legitimately fires twice against
        /// one target still produces two distinct identities.
        /// </summary>
        [TestMethod]
        public void NextAdvancesTheOccurrenceWithinAScopeTest()
        {
            FindingIdentity identity = new FindingIdentity();

            Assert.AreEqual("TM1:d:t:0", identity.Next("TM1", "d", "t"));
            Assert.AreEqual("TM1:d:t:1", identity.Next("TM1", "d", "t"));
            Assert.AreEqual("TM1:d:t:2", identity.Next("TM1", "d", "t"));
        }

        /// <summary>
        /// Counters are per scope, not global. This is what makes an identity independent of how many
        /// other findings a run produced, and therefore stable when an unrelated rule is enabled.
        /// </summary>
        [TestMethod]
        public void NextCountsEachScopeIndependentlyTest()
        {
            FindingIdentity identity = new FindingIdentity();

            Assert.AreEqual("TM1:d:a:0", identity.Next("TM1", "d", "a"));
            Assert.AreEqual("TM2:d:a:0", identity.Next("TM2", "d", "a"));
            Assert.AreEqual("TM1:d:b:0", identity.Next("TM1", "d", "b"));
            Assert.AreEqual("TM1:e:a:0", identity.Next("TM1", "e", "a"));
            Assert.AreEqual("TM1:d:a:1", identity.Next("TM1", "d", "a"));
        }

        /// <summary>
        /// An element id containing the separator cannot impersonate a different finding. Authors
        /// choose their own ids in canonical model JSON, so the separator has to be escaped or two
        /// unrelated findings could collapse onto one identity.
        /// </summary>
        [TestMethod]
        public void SegmentsEscapeTheSeparatorTest()
        {
            string targetHasColon = FindingIdentity.Format("TM1", "d", "a:b", 0);
            string diagramHasColon = FindingIdentity.Format("TM1", "d:a", "b", 0);

            Assert.AreEqual("TM1:d:a%3Ab:0", targetHasColon);
            Assert.AreEqual("TM1:d%3Aa:b:0", diagramHasColon);
            Assert.AreNotEqual(targetHasColon, diagramHasColon);
        }

        /// <summary>
        /// The escape character is itself escaped, so an id that already contains a percent sequence
        /// cannot forge one.
        /// </summary>
        [TestMethod]
        public void SegmentsEscapeThePercentSignTest()
        {
            Assert.AreNotEqual(
                FindingIdentity.Format("TM1", "d", "a%3Ab", 0),
                FindingIdentity.Format("TM1", "d", "a:b", 0));
        }

        /// <summary>
        /// The scope is the identity without the occurrence, which is what a caller reconciles on when
        /// triage has to survive one of several same-scope findings being cleared.
        /// </summary>
        [TestMethod]
        public void ScopeIsTheIdentityWithoutTheOccurrenceTest()
        {
            string scope = FindingIdentity.Scope("TM1014", "page-1", "store-1");

            Assert.AreEqual("TM1014:page-1:store-1", scope);
            Assert.AreEqual(scope + ":0", FindingIdentity.Format("TM1014", "page-1", "store-1", 0));
        }

        /// <summary>A negative occurrence is a caller bug rather than a value to encode.</summary>
        [TestMethod]
        public void FormatRejectsANegativeOccurrenceTest()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FindingIdentity.Format("TM1", "d", "t", -1));
        }
    }
}
