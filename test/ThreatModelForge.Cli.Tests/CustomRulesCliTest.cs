namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// End-to-end tests for the <c>--rules</c> option on <c>analyze</c>: a declarative custom rule is
    /// loaded, evaluated, and gates the exit code alongside the built-in rules.
    /// </summary>
    [TestClass]
    public class CustomRulesCliTest
    {
        private const string ModelAllPacksDisabled =
            "{\"schema\":\"tmforge-json\",\"version\":\"0.1\"," +
            "\"elements\":[" +
            "{\"id\":\"p1\",\"kind\":\"process\",\"name\":\"API\",\"x\":100,\"y\":100}," +
            "{\"id\":\"ds1\",\"kind\":\"datastore\",\"name\":\"DB\",\"x\":400,\"y\":100}]," +
            "\"flows\":[{\"id\":\"f1\",\"source\":\"p1\",\"target\":\"ds1\",\"name\":\"query\"}]," +
            "\"analysis\":{\"disabledPacks\":[\"core-hygiene\",\"stride-completeness\",\"input-validation\",\"data-protection\",\"transport-security\",\"identity-access\"]}}";

        private const string DataStoreEncryptionSpec =
            "{\"rules\":[{\"id\":\"ACME900\",\"severity\":\"error\",\"appliesTo\":\"datastore\"," +
            "\"message\":\"{name} must declare encryption\",\"assert\":{\"property\":\"Encrypted\",\"present\":true}}]}";

        private const string ExternalOnlySpec =
            "{\"rules\":[{\"id\":\"ACME901\",\"severity\":\"error\",\"appliesTo\":\"external\"," +
            "\"message\":\"{name} must authenticate\",\"assert\":{\"property\":\"AuthenticatesItself\",\"equals\":\"Yes\"}}]}";

        /// <summary>
        /// Gets or sets the working directory created for each test.
        /// </summary>
        private string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Creates an isolated working directory for the test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            this.WorkingDirectory = Path.Join(Path.GetTempPath(), "tmforge-customrules-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.WorkingDirectory);
        }

        /// <summary>
        /// Removes the working directory after the test.
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(this.WorkingDirectory))
            {
                Directory.Delete(this.WorkingDirectory, recursive: true);
            }
        }

        /// <summary>
        /// A custom error-severity rule that fires on the model gates the exit code (2), even though
        /// every built-in rule pack is disabled by the model.
        /// </summary>
        [TestMethod]
        public void CustomRuleFiresViaRulesOption()
        {
            string model = this.Write("model.tmforge.json", ModelAllPacksDisabled);
            string spec = this.Write("rules.tmrules.json", DataStoreEncryptionSpec);

            (int exit, _) = Run(new[] { "--rules", spec, model });

            Assert.AreEqual(2, exit);
        }

        /// <summary>
        /// Without <c>--rules</c>, the same model analyzes clean (exit 0) because its built-in packs
        /// are disabled and no custom rule is loaded.
        /// </summary>
        [TestMethod]
        public void WithoutRulesOptionModelIsClean()
        {
            string model = this.Write("model.tmforge.json", ModelAllPacksDisabled);

            (int exit, _) = Run(new[] { model });

            Assert.AreEqual(0, exit);
        }

        /// <summary>
        /// A custom rule that matches no element in the model does not gate the exit code, proving the
        /// rule is actually evaluated rather than firing unconditionally.
        /// </summary>
        [TestMethod]
        public void CustomRuleThatMatchesNothingKeepsExitZero()
        {
            string model = this.Write("model.tmforge.json", ModelAllPacksDisabled);
            string spec = this.Write("rules.tmrules.json", ExternalOnlySpec);

            (int exit, _) = Run(new[] { "--rules", spec, model });

            Assert.AreEqual(0, exit);
        }

        private static (int Exit, string Stdout) Run(string[] args)
        {
            using StringWriter outWriter = new StringWriter();
            using StringWriter errorWriter = new StringWriter();
            TextWriter originalOut = Console.Out;
            TextWriter originalError = Console.Error;
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);
            try
            {
                int exit = AnalyzeCommand.Run(args);
                return (exit, outWriter.ToString());
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }
        }

        private string Write(string fileName, string content)
        {
            string path = Path.Join(this.WorkingDirectory, fileName);
            File.WriteAllText(path, content);
            return path;
        }
    }
}
