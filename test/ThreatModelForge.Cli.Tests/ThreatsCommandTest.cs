namespace ThreatModelForge.Cli.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Integration tests for the <c>tmforge threats</c> and <c>tmforge accept</c> commands: threats are
    /// the persisted, triaged view of the validation findings — persistence, idempotency, and
    /// acceptance through the real CLI entry points.
    /// </summary>
    [TestClass]
    public class ThreatsCommandTest
    {
        /// <summary>Gets or sets the working directory created for each test.</summary>
        private string WorkingDirectory { get; set; } = string.Empty;

        /// <summary>Gets the path of the model created by <see cref="NewModelWithFlow"/>.</summary>
        private string ModelPath => Path.Combine(this.WorkingDirectory, "model.tm7");

        /// <summary>Creates an isolated working directory for the test.</summary>
        [TestInitialize]
        public void Initialize()
        {
            this.WorkingDirectory = Path.Combine(Path.GetTempPath(), "tmforge-threats-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.WorkingDirectory);
        }

        /// <summary>Removes the working directory after the test.</summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(this.WorkingDirectory))
            {
                Directory.Delete(this.WorkingDirectory, recursive: true);
            }
        }

        /// <summary>Threats are reported from the validation findings (an unauthenticated external → TM1023).</summary>
        [TestMethod]
        public void ThreatsReportsThreatsFromFindings()
        {
            string path = this.NewModelWithFlow();

            (int exit, string stdout) = Capture(() => ThreatsCommand.Run(new[] { path, "--json" }));

            Assert.AreEqual(0, exit);
            JsonElement data = JsonDocument.Parse(stdout).RootElement.GetProperty("data");
            Assert.IsTrue(data.GetProperty("summary").GetProperty("count").GetInt32() > 0);
            Assert.IsTrue(HasRule(data, "TM1023"));
        }

        /// <summary>Writing persists the threats into the model's register.</summary>
        [TestMethod]
        public void ThreatsWritePersistsRegister()
        {
            string path = this.NewModelWithFlow();

            (int exit, string stdout) = Capture(() => ThreatsCommand.Run(new[] { path, "--write", "--json" }));

            Assert.AreEqual(0, exit);
            JsonElement data = JsonDocument.Parse(stdout).RootElement.GetProperty("data");
            int added = data.GetProperty("summary").GetProperty("written").GetProperty("added").GetInt32();
            Assert.IsTrue(added > 0);
            (ThreatModel model, _) = CliModelLoader.Load(path);
            Assert.AreEqual(added, model.AllThreatsDictionary.Count);
        }

        /// <summary>Writing twice is idempotent — nothing is added the second time.</summary>
        [TestMethod]
        public void ThreatsWriteIsIdempotent()
        {
            string path = this.NewModelWithFlow();
            Capture(() => ThreatsCommand.Run(new[] { path, "--write" }));

            (int exit, string stdout) = Capture(() => ThreatsCommand.Run(new[] { path, "--write", "--json" }));

            Assert.AreEqual(0, exit);
            JsonElement data = JsonDocument.Parse(stdout).RootElement.GetProperty("data");
            Assert.AreEqual(0, data.GetProperty("summary").GetProperty("written").GetProperty("added").GetInt32());
        }

        /// <summary>Accepting a persisted threat marks it not-applicable with the justification.</summary>
        [TestMethod]
        public void AcceptMarksThreatNotApplicable()
        {
            string path = this.NewModelWithFlow();
            (_, string writeOut) = Capture(() => ThreatsCommand.Run(new[] { path, "--write", "--json" }));
            JsonElement data = JsonDocument.Parse(writeOut).RootElement.GetProperty("data");
            string id = data.GetProperty("threats").EnumerateArray().First().GetProperty("id").GetString() ?? string.Empty;

            (int exit, _) = Capture(() => AcceptCommand.Run(new[] { path, "--threat", id, "--reason", "Accepted for test" }));

            Assert.AreEqual(0, exit);
            (ThreatModel model, _) = CliModelLoader.Load(path);
            Assert.IsTrue(model.AllThreatsDictionary.TryGetValue(id, out Threat? threat));
            Assert.AreEqual(ThreatState.NotApplicable, threat!.State);
            Assert.AreEqual("Accepted for test", threat.StateInformation);
        }

        /// <summary>Accepting without a reason is reported as an error.</summary>
        [TestMethod]
        public void AcceptWithoutReasonReturnsError()
        {
            string path = this.NewModelWithFlow();

            (int exit, _) = Capture(() => AcceptCommand.Run(new[] { path, "--threat", "anything" }));

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

        private static bool HasRule(JsonElement data, string ruleId)
        {
            foreach (JsonElement threat in data.GetProperty("threats").EnumerateArray())
            {
                if (threat.GetProperty("ruleId").GetString() == ruleId)
                {
                    return true;
                }
            }

            return false;
        }

        private string NewModelWithFlow()
        {
            Capture(() => NewCommand.Run(new[] { this.ModelPath, "--name", "Threats Test" }));
            string external = this.AddElement("external", "Client");
            string process = this.AddElement("process", "Gateway");
            this.Connect(external, process);
            return this.ModelPath;
        }

        private string AddElement(string kind, string name)
        {
            (int exit, string stdout) = Capture(() => AddCommand.Run(new[] { kind, this.ModelPath, "--name", name, "--json" }));
            Assert.AreEqual(0, exit);
            return JsonDocument.Parse(stdout).RootElement.GetProperty("data").GetProperty("id").GetGuid().ToString();
        }

        private string Connect(string source, string target)
        {
            (int exit, string stdout) = Capture(() => ConnectCommand.Run(new[] { this.ModelPath, "--source", source, "--target", target, "--json" }));
            Assert.AreEqual(0, exit);
            return JsonDocument.Parse(stdout).RootElement.GetProperty("data").GetProperty("id").GetGuid().ToString();
        }
    }
}
