namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for the <c>tmforge properties</c> command (the typed custom-property schema).
    /// </summary>
    [TestClass]
    public class PropertiesCommandTest
    {
        /// <summary>
        /// Verifies that the schema lists as an aligned table including a known property.
        /// </summary>
        [TestMethod]
        public void ListsSchemaAsTable()
        {
            (int exit, string stdout) = Capture(() => PropertiesCommand.Run(Array.Empty<string>()));

            Assert.AreEqual(0, exit);
            StringAssert.Contains(stdout, "PROPERTY");
            StringAssert.Contains(stdout, "Channel");
        }

        /// <summary>
        /// Verifies that <c>--base</c> restricts the listing to a single DFD primitive.
        /// </summary>
        [TestMethod]
        public void BaseFilterLimitsResults()
        {
            (int exit, string stdout) = Capture(() => PropertiesCommand.Run(new[] { "--base", "flow" }));

            Assert.AreEqual(0, exit);
            StringAssert.Contains(stdout, "Channel");
            Assert.IsFalse(stdout.Contains("RunningAs"), "the flow base must not include process-only properties");
        }

        /// <summary>
        /// Verifies that <c>--json</c> emits the bases and the descriptors, tagged by base primitive.
        /// </summary>
        [TestMethod]
        public void JsonIncludesBasesAndProperties()
        {
            (int exit, string stdout) = Capture(() => PropertiesCommand.Run(new[] { "--json" }));

            Assert.AreEqual(0, exit);
            using JsonDocument document = JsonDocument.Parse(stdout);
            JsonElement data = document.RootElement.GetProperty("data");
            Assert.IsTrue(data.GetProperty("bases").GetArrayLength() > 0);

            bool foundChannel = false;
            foreach (JsonElement descriptor in data.GetProperty("properties").EnumerateArray())
            {
                if (descriptor.GetProperty("name").GetString() == "Channel")
                {
                    Assert.AreEqual("flow", descriptor.GetProperty("appliesTo").GetString());
                    foundChannel = true;
                }
            }

            Assert.IsTrue(foundChannel, "the schema must include the flow Channel property");
        }

        /// <summary>
        /// Verifies that filtering by an unknown base is reported as an error.
        /// </summary>
        [TestMethod]
        public void UnknownBaseReturnsError()
        {
            (int exit, _) = Capture(() => PropertiesCommand.Run(new[] { "--base", "does-not-exist" }));

            Assert.AreEqual(1, exit);
        }

        /// <summary>
        /// Verifies that <c>--json</c> carries the rule policy (rule id, severity, flagged values)
        /// declared by the rules, so an agent can pick safe values in one shot.
        /// </summary>
        [TestMethod]
        public void JsonIncludesRulePolicy()
        {
            (int exit, string stdout) = Capture(() => PropertiesCommand.Run(new[] { "--base", "datastore", "--json" }));

            Assert.AreEqual(0, exit);
            using JsonDocument document = JsonDocument.Parse(stdout);
            JsonElement properties = document.RootElement.GetProperty("data").GetProperty("properties");

            bool checkedAlgorithm = false;
            foreach (JsonElement descriptor in properties.EnumerateArray())
            {
                if (descriptor.GetProperty("name").GetString() != "Algorithm")
                {
                    continue;
                }

                bool referencesWeakCipherRule = false;
                foreach (JsonElement rule in descriptor.GetProperty("rules").EnumerateArray())
                {
                    if (rule.GetProperty("id").GetString() == "TM1025" && rule.GetProperty("severity").GetString() == "Warning")
                    {
                        referencesWeakCipherRule = true;
                    }
                }

                bool flagsSecretbox = false;
                foreach (JsonElement value in descriptor.GetProperty("flagged").EnumerateArray())
                {
                    if (value.GetString() == "secretbox")
                    {
                        flagsSecretbox = true;
                    }
                }

                Assert.IsTrue(referencesWeakCipherRule, "Algorithm must reference TM1025 (Warning)");
                Assert.IsTrue(flagsSecretbox, "Algorithm must flag secretbox");
                checkedAlgorithm = true;
            }

            Assert.IsTrue(checkedAlgorithm, "datastore must include the Algorithm property");
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
