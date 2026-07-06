namespace ThreatModelForge.Cli.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Analysis;

    /// <summary>
    /// Unit tests for <see cref="ConsoleMessageWriter"/> severity-threshold gating (the lint exit
    /// code, see the <c>--max-severity</c> option).
    /// </summary>
    [TestClass]
    public class ConsoleMessageWriterTest
    {
        /// <summary>
        /// A finding gates only thresholds at or below its severity (Error is the most severe).
        /// </summary>
        [TestMethod]
        public void MeetsThresholdReflectsMostSevereFinding()
        {
            ConsoleMessageWriter writer = new ConsoleMessageWriter("test", quiet: true);
            Assert.IsFalse(writer.MeetsThreshold(MessageSeverity.Error), "no findings recorded yet");

            writer.WriteCore(MessageSeverity.Warning, "TM9999", "a warning");
            Assert.IsFalse(writer.MeetsThreshold(MessageSeverity.Error), "a warning must not gate an error threshold");
            Assert.IsTrue(writer.MeetsThreshold(MessageSeverity.Warning), "a warning meets the warning threshold");
            Assert.IsTrue(writer.MeetsThreshold(MessageSeverity.Info), "a warning meets the info threshold");

            writer.WriteCore(MessageSeverity.Error, "TM9998", "an error");
            Assert.IsTrue(writer.MeetsThreshold(MessageSeverity.Error), "an error meets the error threshold");
        }
    }
}
