namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;

    /// <summary>
    /// Unit tests for the <see cref="UnauditedBoundaryProcessRule"/> class (TM1029).
    /// </summary>
    [TestClass]
    public class UnauditedBoundaryProcessRuleTests
    {
        private const string ProcessGenericTypeId = "GE.P";

        private const string StorageComponentGenericTypeId = "GE.DS";

        /// <summary>
        /// Verifies the rule's identity and populated metadata.
        /// </summary>
        [TestMethod]
        public void ConstructorTest()
        {
            using UnauditedBoundaryProcessRule target = new UnauditedBoundaryProcessRule();
            Assert.AreEqual("TM1029", target.ID);
            Assert.AreEqual(MessageSeverity.Warning, target.Severity);
            Assert.IsNotNull(target.HelpUri);
            Assert.IsFalse(string.IsNullOrEmpty(target.FullDescription));
            Assert.IsFalse(string.IsNullOrEmpty(target.HelpText));
        }

        /// <summary>
        /// A boundary-facing process with no reachable audit-log store is flagged.
        /// </summary>
        [TestMethod]
        public void FlagsBoundaryProcessWithoutAuditLog()
        {
            StencilEllipse process = CreateProcess("Order Service");
            BorderBoundary border = CreateBoundary();
            Connector inbound = CrossingInbound(process);
            ThreatModel model = BuildModel(border, process, inbound);

            MockMessageWriter writer = Evaluate(model);

            Assert.AreEqual(1, writer.Messages.Count);
            Assert.AreSame(process, writer.Messages[0].Target);
            Assert.IsTrue(writer.Messages[0].Text!.Contains("Order Service"));
        }

        /// <summary>
        /// A boundary-facing process that writes to an audit-log store is not flagged.
        /// </summary>
        [TestMethod]
        public void IgnoresBoundaryProcessThatWritesToAuditLog()
        {
            StencilEllipse process = CreateProcess("Order Service");
            BorderBoundary border = CreateBoundary();
            StencilParallelLines log = CreateStore("Audit Log", ("StoresLogData", "Yes"));
            Connector inbound = CrossingInbound(process);
            Connector toLog = Edge(process.Guid, log.Guid);
            ThreatModel model = BuildModel(border, process, inbound, log, toLog);

            MockMessageWriter writer = Evaluate(model);

            Assert.AreEqual(0, writer.Messages.Count);
        }

        /// <summary>
        /// A boundary-facing process that writes only to a non-log store is still flagged.
        /// </summary>
        [TestMethod]
        public void FlagsBoundaryProcessWritingOnlyToNonLogStore()
        {
            StencilEllipse process = CreateProcess("Order Service");
            BorderBoundary border = CreateBoundary();
            StencilParallelLines store = CreateStore("Orders", ("StoresLogData", "No"));
            Connector inbound = CrossingInbound(process);
            Connector toStore = Edge(process.Guid, store.Guid);
            ThreatModel model = BuildModel(border, process, inbound, store, toStore);

            MockMessageWriter writer = Evaluate(model);

            Assert.AreEqual(1, writer.Messages.Count);
        }

        /// <summary>
        /// A process whose inbound edge does not cross a trust boundary is not flagged.
        /// </summary>
        [TestMethod]
        public void IgnoresProcessWithoutCrossBoundaryInput()
        {
            StencilEllipse process = CreateProcess("Order Service");
            BorderBoundary border = CreateBoundary();
            Connector inbound = NonCrossingInbound(process);
            ThreatModel model = BuildModel(border, process, inbound);

            MockMessageWriter writer = Evaluate(model);

            Assert.AreEqual(0, writer.Messages.Count);
        }

        private static MockMessageWriter Evaluate(ThreatModel model)
        {
            MockMessageWriter writer = new MockMessageWriter();
            using (UnauditedBoundaryProcessRule target = new UnauditedBoundaryProcessRule())
            {
                target.Evaluate(new RuleEvaluationContext(model, writer));
            }

            return writer;
        }

        private static StencilEllipse CreateProcess(string name)
        {
            StencilEllipse process = new StencilEllipse
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = ProcessGenericTypeId,
            };
            process.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = name });
            return process;
        }

        private static StencilParallelLines CreateStore(string name, params (string Name, string Value)[] properties)
        {
            StencilParallelLines store = new StencilParallelLines
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = StorageComponentGenericTypeId,
            };
            store.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = name });
            foreach ((string propertyName, string propertyValue) in properties)
            {
                store.Properties.Add(new CustomStringDisplayAttribute { Value = $"{propertyName}:{propertyValue}" });
            }

            return store;
        }

        private static BorderBoundary CreateBoundary()
        {
            return new BorderBoundary { Guid = Guid.NewGuid(), Left = 5, Top = 10, Height = 15, Width = 20 };
        }

        private static Connector CrossingInbound(StencilEllipse process)
        {
            return new Connector
            {
                Guid = Guid.NewGuid(),
                SourceX = 0,
                SourceY = 0,
                TargetX = 10,
                TargetY = 11,
                TargetGuid = process.Guid,
            };
        }

        private static Connector NonCrossingInbound(StencilEllipse process)
        {
            return new Connector
            {
                Guid = Guid.NewGuid(),
                SourceX = 8,
                SourceY = 12,
                TargetX = 10,
                TargetY = 11,
                TargetGuid = process.Guid,
            };
        }

        private static Connector Edge(Guid source, Guid target)
        {
            return new Connector { Guid = Guid.NewGuid(), SourceGuid = source, TargetGuid = target };
        }

        private static ThreatModel BuildModel(BorderBoundary border, StencilEllipse process, params object[] elements)
        {
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Header = "DFD-0" };
            diagram.Borders.Add(border.Guid, border);
            diagram.Borders.Add(process.Guid, process);
            foreach (object element in elements)
            {
                switch (element)
                {
                    case Connector connector:
                        diagram.Lines.Add(connector.Guid, connector);
                        break;
                    case StencilParallelLines store:
                        diagram.Borders.Add(store.Guid, store);
                        break;
                }
            }

            return new ThreatModel { DrawingSurfaceList = { diagram } };
        }
    }
}
