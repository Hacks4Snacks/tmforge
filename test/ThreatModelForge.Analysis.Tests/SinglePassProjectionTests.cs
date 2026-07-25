namespace ThreatModelForge.Analysis.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Unit tests for the single-pass analysis seam: one rule-set evaluation produces the messages that
    /// both the finding projection and the threat projection read. The counting rule is the point —
    /// output alone cannot distinguish one evaluation from two, so the test observes the evaluation
    /// itself.
    /// </summary>
    [TestClass]
    public class SinglePassProjectionTests
    {
        /// <summary>
        /// Deriving findings and threats from one evaluation calls every enabled rule exactly once.
        /// </summary>
        [TestMethod]
        public void OneEvaluationFeedsBothProjections()
        {
            CountingRule hygiene = new CountingRule(9001, threatBearing: false);
            CountingRule threatBearing = new CountingRule(9002, threatBearing: true);
            using RuleSet ruleSet = new RuleSet();
            ruleSet.Rules.Add(hygiene);
            ruleSet.Rules.Add(threatBearing);

            FindingCollector collector = new FindingCollector();
            ruleSet.Evaluate(new RuleEvaluationContext(SingleProcessModel(), collector));

            IReadOnlyList<Message> findings = collector.Messages;
            GenerationResult threats = ThreatGenerator.Project(collector.Messages);

            Assert.AreEqual(1, hygiene.Evaluations, "every enabled rule runs exactly once");
            Assert.AreEqual(1, threatBearing.Evaluations, "every enabled rule runs exactly once");
            Assert.AreEqual(2, findings.Count, "every message is a finding");
            Assert.AreEqual(1, threats.Count, "only threat-bearing rules project threats");
            Assert.AreEqual("TM9002", threats.Threats[0].RuleId);
        }

        /// <summary>
        /// Projecting threats from already-collected messages never re-evaluates the rules. This is what
        /// lets one caller derive both projections from a single detection pass.
        /// </summary>
        [TestMethod]
        public void ProjectDoesNotEvaluateRules()
        {
            CountingRule rule = new CountingRule(9003, threatBearing: true);
            using RuleSet ruleSet = new RuleSet();
            ruleSet.Rules.Add(rule);

            FindingCollector collector = new FindingCollector();
            ruleSet.Evaluate(new RuleEvaluationContext(SingleProcessModel(), collector));
            Assert.AreEqual(1, rule.Evaluations);

            _ = ThreatGenerator.Project(collector.Messages);
            _ = ThreatGenerator.Project(collector.Messages);

            Assert.AreEqual(1, rule.Evaluations, "projection reads messages; it does not detect");
        }

        /// <summary>
        /// Projecting from one evaluation matches what the evaluate-and-project helper produces, so the
        /// shared path and the standalone path cannot drift.
        /// </summary>
        [TestMethod]
        public void ProjectMatchesGenerate()
        {
            ThreatModel model = SingleProcessModel();
            using RuleSet generated = new RuleSet();
            generated.Rules.Add(new CountingRule(9004, threatBearing: true));
            GenerationResult viaGenerate = ThreatGenerator.Generate(model, generated);

            using RuleSet projected = new RuleSet();
            projected.Rules.Add(new CountingRule(9004, threatBearing: true));
            FindingCollector collector = new FindingCollector();
            projected.Evaluate(new RuleEvaluationContext(model, collector));
            GenerationResult viaProject = ThreatGenerator.Project(collector.Messages);

            CollectionAssert.AreEqual(
                viaGenerate.Threats.Select(threat => threat.Id).ToList(),
                viaProject.Threats.Select(threat => threat.Id).ToList());
        }

        /// <summary>A disabled rule is not evaluated at all, in either projection.</summary>
        [TestMethod]
        public void DisabledRulesAreNotEvaluated()
        {
            CountingRule rule = new CountingRule(9005, threatBearing: true);
            using RuleSet ruleSet = new RuleSet();
            ruleSet.Rules.Add(rule);
            ruleSet.Disable(new[] { CountingRule.PackId }, null);

            FindingCollector collector = new FindingCollector();
            ruleSet.Evaluate(new RuleEvaluationContext(SingleProcessModel(), collector));

            Assert.AreEqual(0, rule.Evaluations);
            Assert.AreEqual(0, collector.Messages.Count);
            Assert.AreEqual(0, ThreatGenerator.Project(collector.Messages).Count);
        }

        private static ThreatModel SingleProcessModel()
        {
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Guid = System.Guid.NewGuid(), Header = "DFD-0" };
            StencilEllipse process = new StencilEllipse
            {
                Guid = System.Guid.NewGuid(),
                TypeId = "GE.P",
                GenericTypeId = "GE.P",
            };
            process.Properties.Add(new StringDisplayAttribute { Name = "Name", DisplayName = "Name", Value = "Gateway" });
            diagram.Borders.Add(process.Guid, process);
            return new ThreatModel { DrawingSurfaceList = { diagram } };
        }

        /// <summary>
        /// A rule that records how many times it was evaluated and raises one message per process, so a
        /// test can count evaluations rather than infer them from output.
        /// </summary>
        private sealed class CountingRule : Rule
        {
            public const string PackId = "counting";

            private readonly bool threatBearing;

            public CountingRule(int id, bool threatBearing)
                : base(id, MessageSeverity.Warning, PackId)
            {
                this.threatBearing = threatBearing;
                this.FullDescription = "Counting rule.";
                this.HelpText = "Counting rule.";
            }

            public int Evaluations { get; private set; }

            public override StrideCategory? Stride => this.threatBearing ? StrideCategory.Spoofing : null;

            public override IReadOnlyList<ThreatReference> ThreatReferences => this.threatBearing
                ? new[] { ThreatReference.Cwe(287) }
                : System.Array.Empty<ThreatReference>();

            public override void Evaluate(RuleEvaluationContext context)
            {
                this.Evaluations++;
                foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
                {
                    foreach (object candidate in diagram.Borders.Values)
                    {
                        if (candidate is Entity entity)
                        {
                            context.Writer.Write(new Message
                            {
                                Severity = MessageSeverity.Warning,
                                Source = this,
                                Target = entity,
                                Model = diagram,
                                Text = "counted",
                            });
                        }
                    }
                }
            }
        }
    }
}
