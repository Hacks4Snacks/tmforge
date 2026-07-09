namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for the <see cref="GitSetupCommand"/> class (the side-effect-free <c>--print</c> path).
    /// </summary>
    [TestClass]
    public class GitSetupCommandTest
    {
        /// <summary>
        /// Verifies that <c>--print</c> shows the local (per-repository) config commands and the
        /// <c>.git/info/attributes</c> mapping, without touching git.
        /// </summary>
        [TestMethod]
        public void PrintShowsLocalConfigCommands()
        {
            (int exit, string output) = Capture(new[] { "--print" });

            Assert.AreEqual(0, exit);
            StringAssert.Contains(output, "git config diff.tmforge.textconv");
            StringAssert.Contains(output, "git config merge.tmforge.driver");
            StringAssert.Contains(output, "*.tm7 diff=tmforge merge=tmforge");
            StringAssert.Contains(output, "info/attributes");
        }

        /// <summary>
        /// Verifies that <c>--print --global</c> emits global-scoped config and the attributes-file setup.
        /// </summary>
        [TestMethod]
        public void PrintGlobalShowsGlobalScope()
        {
            (int exit, string output) = Capture(new[] { "--print", "--global" });

            Assert.AreEqual(0, exit);
            StringAssert.Contains(output, "git config --global merge.tmforge.driver");
            StringAssert.Contains(output, "core.attributesFile");
        }

        private static (int Exit, string Output) Capture(string[] args)
        {
            using StringWriter writer = new StringWriter();
            TextWriter original = Console.Out;
            Console.SetOut(writer);
            try
            {
                int exit = GitSetupCommand.Run(args);
                return (exit, writer.ToString());
            }
            finally
            {
                Console.SetOut(original);
            }
        }
    }
}
