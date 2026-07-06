namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for the <c>tmforge stencils</c> command (the authoring stencil catalog).
    /// </summary>
    [TestClass]
    public class StencilsCommandTest
    {
        /// <summary>
        /// Verifies that the catalog lists as an aligned table including a known stencil id.
        /// </summary>
        [TestMethod]
        public void ListsCatalogAsTable()
        {
            (int exit, string stdout) = Capture(() => StencilsCommand.Run(Array.Empty<string>()));

            Assert.AreEqual(0, exit);
            StringAssert.Contains(stdout, "ID");
            StringAssert.Contains(stdout, "azure-sql");
        }

        /// <summary>
        /// Verifies that <c>--pack</c> restricts the listing to a single pack.
        /// </summary>
        [TestMethod]
        public void PackFilterLimitsResults()
        {
            (int exit, string stdout) = Capture(() => StencilsCommand.Run(new[] { "--pack", "azure" }));

            Assert.AreEqual(0, exit);
            StringAssert.Contains(stdout, "azure-sql");
            Assert.IsFalse(stdout.Contains("k8s-"), "the azure pack must not include kubernetes stencils");
        }

        /// <summary>
        /// Verifies that <c>--json</c> emits both the packs and the stencils, with the base primitive.
        /// </summary>
        [TestMethod]
        public void JsonIncludesStencilsAndPacks()
        {
            (int exit, string stdout) = Capture(() => StencilsCommand.Run(new[] { "--json" }));

            Assert.AreEqual(0, exit);
            using JsonDocument document = JsonDocument.Parse(stdout);
            JsonElement data = document.RootElement.GetProperty("data");
            Assert.IsTrue(data.GetProperty("packs").GetArrayLength() > 0);

            bool foundAzureSql = false;
            foreach (JsonElement stencil in data.GetProperty("stencils").EnumerateArray())
            {
                if (stencil.GetProperty("id").GetString() == "azure-sql")
                {
                    Assert.AreEqual("datastore", stencil.GetProperty("base").GetString());
                    foundAzureSql = true;
                }
            }

            Assert.IsTrue(foundAzureSql, "the catalog must include the azure-sql stencil");
        }

        /// <summary>
        /// Verifies that filtering by an unknown pack is reported as an error.
        /// </summary>
        [TestMethod]
        public void UnknownPackReturnsError()
        {
            (int exit, _) = Capture(() => StencilsCommand.Run(new[] { "--pack", "does-not-exist" }));

            Assert.AreEqual(1, exit);
        }

        private static (int Exit, string Stdout) Capture(Func<int> run)
        {
            StringWriter outWriter = new StringWriter();
            StringWriter errorWriter = new StringWriter();
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);
            try
            {
                int exit = run();
                return (exit, outWriter.ToString());
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }
        }
    }
}
