namespace ThreatModelForge.Cli.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Analysis;

    /// <summary>
    /// Unit test for the <see cref="AnalyzeArguments"/> class.
    /// </summary>
    [TestClass]
    public class AnalyzeArgumentsTests
    {
        /// <summary>
        /// Unit test for the <see cref="AnalyzeArguments.TryParse(string[], out AnalyzeArguments)"/> method.
        /// </summary>
        [TestMethod]
        public void TryParseTest()
        {
            Assert.IsTrue(AnalyzeArguments.TryParse(
                new string[] { "foo.tm7" },
                out AnalyzeArguments? actual));
            Assert.IsNotNull(actual);
            Assert.AreEqual("foo.tm7", actual!.Path);

            Assert.IsTrue(AnalyzeArguments.TryParse(
                new string[] { "--define", "foo=123", "foo.tm7" },
                out actual));
            Assert.IsNotNull(actual);
            Assert.AreEqual("foo.tm7", actual!.Path);
            Assert.AreEqual("123", actual.Variables["Foo"]);

            Assert.IsTrue(AnalyzeArguments.TryParse(
                new string[] { "--define", "foo=123", "--define", "bar=abc", "foo.tm7" },
                out actual));
            Assert.IsNotNull(actual);
            Assert.AreEqual("foo.tm7", actual!.Path);
            Assert.AreEqual("123", actual.Variables["Foo"]);
            Assert.AreEqual("abc", actual.Variables["Bar"]);

            Assert.IsTrue(AnalyzeArguments.TryParse(
                new string[] { "--define", "foo=123", "--ruleset", "MyRules.ruleset", "foo.tm7" },
                out actual));
            Assert.IsNotNull(actual);
            Assert.AreEqual("foo.tm7", actual!.Path);
            Assert.AreEqual("MyRules.ruleset", actual.RuleSetPath);
            Assert.AreEqual("123", actual.Variables["Foo"]);

            Assert.IsTrue(AnalyzeArguments.TryParse(
                new string[] { "--define", "foo=123", "--suppressionFile", "MySuppress.json", "foo.tm7" },
                out actual));
            Assert.IsNotNull(actual);
            Assert.AreEqual("foo.tm7", actual!.Path);
            Assert.AreEqual("MySuppress.json", actual.SuppressionFilePath);
            Assert.AreEqual("123", actual.Variables["Foo"]);
        }

        /// <summary>
        /// The <c>--max-severity</c> option defaults to <see cref="MessageSeverity.Error"/>, accepts
        /// error/warning/info, and rejects unknown levels.
        /// </summary>
        [TestMethod]
        public void TryParseMaxSeverityTest()
        {
            Assert.IsTrue(AnalyzeArguments.TryParse(new string[] { "foo.tm7" }, out AnalyzeArguments? actual));
            Assert.AreEqual(MessageSeverity.Error, actual!.MaxSeverity);

            Assert.IsTrue(AnalyzeArguments.TryParse(new string[] { "--max-severity", "warning", "foo.tm7" }, out actual));
            Assert.AreEqual(MessageSeverity.Warning, actual!.MaxSeverity);

            Assert.IsTrue(AnalyzeArguments.TryParse(new string[] { "--max-severity", "info", "foo.tm7" }, out actual));
            Assert.AreEqual(MessageSeverity.Info, actual!.MaxSeverity);

            Assert.IsFalse(AnalyzeArguments.TryParse(new string[] { "--max-severity", "bogus", "foo.tm7" }, out _));
        }

        /// <summary>
        /// Unit test for the <see cref="AnalyzeArguments.TryParse(string[], out AnalyzeArguments)"/> method.
        /// </summary>
        [TestMethod]
        public void TryParseNegativeTest()
        {
            Assert.IsFalse(AnalyzeArguments.TryParse(Array.Empty<string>(), out _));
            Assert.IsFalse(AnalyzeArguments.TryParse(new string[] { "--help" }, out _));
            Assert.IsFalse(AnalyzeArguments.TryParse(new string[] { "-?" }, out _));
            Assert.IsFalse(AnalyzeArguments.TryParse(new string[] { "-?", "foo.tm7" }, out _));
            Assert.IsFalse(AnalyzeArguments.TryParse(new string[] { "--define", "A =", "foo.tm7" }, out _));
            Assert.IsFalse(AnalyzeArguments.TryParse(new string[] { "--define", "A==", "foo.tm7" }, out _));
            Assert.IsFalse(AnalyzeArguments.TryParse(new string[] { "--define", "=123", "foo.tm7" }, out _));
            Assert.IsFalse(AnalyzeArguments.TryParse(new string[] { "--bogus", "foo.tm7" }, out _));
            Assert.IsFalse(AnalyzeArguments.TryParse(new string[] { "-invalid", "foo.tm7" }, out _));
            Assert.IsFalse(AnalyzeArguments.TryParse(new string[] { "bar.tm7", "foo.tm7" }, out _));

            // Legacy grammar is not accepted (no backwards compatibility before publication).
            Assert.IsFalse(AnalyzeArguments.TryParse(new string[] { "-Dfoo=123", "foo.tm7" }, out _));
            Assert.IsFalse(AnalyzeArguments.TryParse(new string[] { "-ruleset:MyRules.ruleset", "foo.tm7" }, out _));
        }

        /// <summary>
        /// Unit test for the canonical GNU-style grammar and the <c>--json</c> flag.
        /// </summary>
        [TestMethod]
        public void TryParseCanonicalGrammar()
        {
            Assert.IsTrue(AnalyzeArguments.TryParse(
                new string[] { "--ruleset", "MyRules.ruleset", "foo.tm7" },
                out AnalyzeArguments? actual));
            Assert.AreEqual("MyRules.ruleset", actual!.RuleSetPath);

            Assert.IsTrue(AnalyzeArguments.TryParse(
                new string[] { "--ruleset=MyRules.ruleset", "foo.tm7" },
                out actual));
            Assert.AreEqual("MyRules.ruleset", actual!.RuleSetPath);

            Assert.IsTrue(AnalyzeArguments.TryParse(
                new string[] { "--suppressionFile", "MySuppress.json", "foo.tm7" },
                out actual));
            Assert.AreEqual("MySuppress.json", actual!.SuppressionFilePath);

            Assert.IsTrue(AnalyzeArguments.TryParse(
                new string[] { "--define", "Foo=123", "foo.tm7" },
                out actual));
            Assert.AreEqual("123", actual!.Variables["Foo"]);

            Assert.IsTrue(AnalyzeArguments.TryParse(
                new string[] { "--json", "foo.tm7" },
                out actual));
            Assert.IsTrue(actual!.Json);
            Assert.AreEqual("foo.tm7", actual.Path);

            Assert.IsFalse(AnalyzeArguments.TryParse(new string[] { "--bogus", "foo.tm7" }, out _));
        }
    }
}
