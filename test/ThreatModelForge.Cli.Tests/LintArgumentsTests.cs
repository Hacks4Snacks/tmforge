namespace ThreatModelForge.Cli.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.Analysis;

    /// <summary>
    /// Unit test for the <see cref="LintArguments"/> class.
    /// </summary>
    [TestClass]
    public class LintArgumentsTests
    {
        /// <summary>
        /// Unit test for the <see cref="LintArguments.TryParse(string[], out LintArguments)"/> method.
        /// </summary>
        [TestMethod]
        public void TryParseTest()
        {
            Assert.IsTrue(LintArguments.TryParse(
                new string[] { "foo.tm7" },
                out LintArguments? actual));
            Assert.IsNotNull(actual);
            Assert.AreEqual("foo.tm7", actual!.Path);

            Assert.IsTrue(LintArguments.TryParse(
                new string[] { "--define", "foo=123", "foo.tm7" },
                out actual));
            Assert.IsNotNull(actual);
            Assert.AreEqual("foo.tm7", actual!.Path);
            Assert.AreEqual("123", actual.Variables["Foo"]);

            Assert.IsTrue(LintArguments.TryParse(
                new string[] { "--define", "foo=123", "--define", "bar=abc", "foo.tm7" },
                out actual));
            Assert.IsNotNull(actual);
            Assert.AreEqual("foo.tm7", actual!.Path);
            Assert.AreEqual("123", actual.Variables["Foo"]);
            Assert.AreEqual("abc", actual.Variables["Bar"]);

            Assert.IsTrue(LintArguments.TryParse(
                new string[] { "--define", "foo=123", "--ruleset", "MyRules.ruleset", "foo.tm7" },
                out actual));
            Assert.IsNotNull(actual);
            Assert.AreEqual("foo.tm7", actual!.Path);
            Assert.AreEqual("MyRules.ruleset", actual.RuleSetPath);
            Assert.AreEqual("123", actual.Variables["Foo"]);

            Assert.IsTrue(LintArguments.TryParse(
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
            Assert.IsTrue(LintArguments.TryParse(new string[] { "foo.tm7" }, out LintArguments? actual));
            Assert.AreEqual(MessageSeverity.Error, actual!.MaxSeverity);

            Assert.IsTrue(LintArguments.TryParse(new string[] { "--max-severity", "warning", "foo.tm7" }, out actual));
            Assert.AreEqual(MessageSeverity.Warning, actual!.MaxSeverity);

            Assert.IsTrue(LintArguments.TryParse(new string[] { "--max-severity", "info", "foo.tm7" }, out actual));
            Assert.AreEqual(MessageSeverity.Info, actual!.MaxSeverity);

            Assert.IsFalse(LintArguments.TryParse(new string[] { "--max-severity", "bogus", "foo.tm7" }, out _));
        }

        /// <summary>
        /// Unit test for the <see cref="LintArguments.TryParse(string[], out LintArguments)"/> method.
        /// </summary>
        [TestMethod]
        public void TryParseNegativeTest()
        {
            Assert.IsFalse(LintArguments.TryParse(Array.Empty<string>(), out _));
            Assert.IsFalse(LintArguments.TryParse(new string[] { "--help" }, out _));
            Assert.IsFalse(LintArguments.TryParse(new string[] { "-?" }, out _));
            Assert.IsFalse(LintArguments.TryParse(new string[] { "-?", "foo.tm7" }, out _));
            Assert.IsFalse(LintArguments.TryParse(new string[] { "--define", "A =", "foo.tm7" }, out _));
            Assert.IsFalse(LintArguments.TryParse(new string[] { "--define", "A==", "foo.tm7" }, out _));
            Assert.IsFalse(LintArguments.TryParse(new string[] { "--define", "=123", "foo.tm7" }, out _));
            Assert.IsFalse(LintArguments.TryParse(new string[] { "--bogus", "foo.tm7" }, out _));
            Assert.IsFalse(LintArguments.TryParse(new string[] { "-invalid", "foo.tm7" }, out _));
            Assert.IsFalse(LintArguments.TryParse(new string[] { "bar.tm7", "foo.tm7" }, out _));

            // Legacy grammar is not accepted (no backwards compatibility before publication).
            Assert.IsFalse(LintArguments.TryParse(new string[] { "-Dfoo=123", "foo.tm7" }, out _));
            Assert.IsFalse(LintArguments.TryParse(new string[] { "-ruleset:MyRules.ruleset", "foo.tm7" }, out _));
        }

        /// <summary>
        /// Unit test for the canonical GNU-style grammar and the <c>--json</c> flag.
        /// </summary>
        [TestMethod]
        public void TryParseCanonicalGrammar()
        {
            Assert.IsTrue(LintArguments.TryParse(
                new string[] { "--ruleset", "MyRules.ruleset", "foo.tm7" },
                out LintArguments? actual));
            Assert.AreEqual("MyRules.ruleset", actual!.RuleSetPath);

            Assert.IsTrue(LintArguments.TryParse(
                new string[] { "--ruleset=MyRules.ruleset", "foo.tm7" },
                out actual));
            Assert.AreEqual("MyRules.ruleset", actual!.RuleSetPath);

            Assert.IsTrue(LintArguments.TryParse(
                new string[] { "--suppressionFile", "MySuppress.json", "foo.tm7" },
                out actual));
            Assert.AreEqual("MySuppress.json", actual!.SuppressionFilePath);

            Assert.IsTrue(LintArguments.TryParse(
                new string[] { "--define", "Foo=123", "foo.tm7" },
                out actual));
            Assert.AreEqual("123", actual!.Variables["Foo"]);

            Assert.IsTrue(LintArguments.TryParse(
                new string[] { "--json", "foo.tm7" },
                out actual));
            Assert.IsTrue(actual!.Json);
            Assert.AreEqual("foo.tm7", actual.Path);

            Assert.IsFalse(LintArguments.TryParse(new string[] { "--bogus", "foo.tm7" }, out _));
        }
    }
}
