namespace ThreatModelForge.Analysis.Rules.Tests
{
    using System;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ThreatModelForge.KnowledgeBase;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Conformance tests covering every control-like property consumed by a built-in rule.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A control property carries three distinct states, and collapsing any two of them is a correctness
    /// bug rather than a cosmetic one. An evidenced control (<c>Encrypted = TDE</c>) means the control is
    /// in place. A stated absence (<c>Encrypted = No</c>) means somebody checked and it is not. An
    /// unevidenced value — the property is missing, blank, or set to <c>Unknown</c> — means nobody has
    /// recorded anything at all.
    /// </para>
    /// <para>
    /// Reading an unevidenced value as a control in place is the failure mode these tests exist to
    /// prevent: it hands back a clean report for a system nobody has actually reviewed. So for every
    /// control below, an unevidenced value must still produce the finding, and the finding must say that
    /// the control is unevidenced rather than claim it was confirmed missing.
    /// </para>
    /// </remarks>
    [TestClass]
    public class ControlEvidenceConformanceTest
    {
        private const string ProcessGenericTypeId = "GE.P";

        private const string StorageComponentGenericTypeId = "GE.DS";

        private const string ExternalInteractorGenericTypeId = "GE.EI";

        /// <summary>
        /// Phrase shared by every unevidenced finding, used here to tell the two message variants apart.
        /// </summary>
        private const string UnevidencedMarker = "is not evidenced";

        /// <summary>
        /// Named acceptance criterion: <c>AuthenticationScheme = Unknown</c> cannot suppress an
        /// authentication finding.
        /// </summary>
        [TestMethod]
        public void AuthenticationSchemeUnknownCannotSuppressAuthenticationFindingTest()
        {
            IList<Message> messages = EvaluateBoundaryProcess(
                new UnauthenticatedBoundaryProcessRule(),
                "AuthenticationScheme",
                ControlEvidenceValues.Unknown);

            Assert.AreEqual(1, messages.Count, "Unknown must never read as an authenticated process.");
            Assert.AreEqual(MessageSeverity.Warning, messages[0].Severity);
            AssertUnevidenced(messages);
        }

        /// <summary>
        /// Named acceptance criterion: <c>Encrypted = Unknown</c> cannot suppress an encryption finding.
        /// </summary>
        [TestMethod]
        public void EncryptedUnknownCannotSuppressEncryptionFindingTest()
        {
            IList<Message> messages = EvaluateStore(
                new UnencryptedSecretStoreRule(),
                "StoresCredentials",
                "Yes",
                "Encrypted",
                ControlEvidenceValues.Unknown);

            Assert.AreEqual(1, messages.Count, "Unknown must never read as encrypted at rest.");
            Assert.AreEqual(MessageSeverity.Warning, messages[0].Severity);
            AssertUnevidenced(messages);
        }

        /// <summary>
        /// An unevidenced authentication scheme is reported, and reported as unevidenced.
        /// </summary>
        /// <param name="value">The property value under test, or <see langword="null"/> when absent.</param>
        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("Unknown")]
        [DataRow("unknown")]
        [DataRow("  Unknown  ")]
        public void UnevidencedAuthenticationSchemeIsReportedTest(string? value)
        {
            AssertUnevidenced(EvaluateBoundaryProcess(new UnauthenticatedBoundaryProcessRule(), "AuthenticationScheme", value));
        }

        /// <summary>
        /// A stated absence of authentication is reported as a confirmed absence.
        /// </summary>
        [TestMethod]
        public void AbsentAuthenticationSchemeIsReportedAsAbsenceTest()
        {
            AssertConfirmedAbsence(EvaluateBoundaryProcess(new UnauthenticatedBoundaryProcessRule(), "AuthenticationScheme", "None"));
        }

        /// <summary>
        /// Every evidenced authentication scheme suppresses the finding.
        /// </summary>
        /// <param name="value">The property value under test.</param>
        [TestMethod]
        [DataRow("Basic")]
        [DataRow("OAuth")]
        [DataRow("RBAC")]
        [DataRow("Certificate")]
        [DataRow("ManagedIdentity")]
        public void EvidencedAuthenticationSchemeSuppressesFindingTest(string value)
        {
            AssertNoFinding(EvaluateBoundaryProcess(new UnauthenticatedBoundaryProcessRule(), "AuthenticationScheme", value));
        }

        /// <summary>
        /// An unevidenced isolation mechanism is reported, and reported as unevidenced.
        /// </summary>
        /// <param name="value">The property value under test, or <see langword="null"/> when absent.</param>
        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("Unknown")]
        public void UnevidencedIsolationIsReportedTest(string? value)
        {
            AssertUnevidenced(EvaluateBoundaryProcess(new WeakProcessIsolationRule(), "Isolation", value));
        }

        /// <summary>
        /// A stated absence of isolation is reported as a confirmed absence.
        /// </summary>
        [TestMethod]
        public void AbsentIsolationIsReportedAsAbsenceTest()
        {
            AssertConfirmedAbsence(EvaluateBoundaryProcess(new WeakProcessIsolationRule(), "Isolation", "None"));
        }

        /// <summary>
        /// Every evidenced isolation mechanism suppresses the finding.
        /// </summary>
        /// <param name="value">The property value under test.</param>
        [TestMethod]
        [DataRow("Process")]
        [DataRow("Container")]
        [DataRow("VM")]
        public void EvidencedIsolationSuppressesFindingTest(string value)
        {
            AssertNoFinding(EvaluateBoundaryProcess(new WeakProcessIsolationRule(), "Isolation", value));
        }

        /// <summary>
        /// Unevidenced input sanitization is reported, and reported as unevidenced.
        /// </summary>
        /// <param name="value">The property value under test, or <see langword="null"/> when absent.</param>
        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("Unknown")]
        public void UnevidencedInputSanitizationIsReportedTest(string? value)
        {
            AssertUnevidenced(EvaluateBoundaryProcess(new UnsanitizedCrossBoundaryInputRule(), "SanitizesInput", value));
        }

        /// <summary>
        /// A stated absence of input sanitization is reported as a confirmed absence.
        /// </summary>
        [TestMethod]
        public void AbsentInputSanitizationIsReportedAsAbsenceTest()
        {
            AssertConfirmedAbsence(EvaluateBoundaryProcess(new UnsanitizedCrossBoundaryInputRule(), "SanitizesInput", "No"));
        }

        /// <summary>
        /// Evidenced input sanitization suppresses the finding.
        /// </summary>
        [TestMethod]
        public void EvidencedInputSanitizationSuppressesFindingTest()
        {
            AssertNoFinding(EvaluateBoundaryProcess(new UnsanitizedCrossBoundaryInputRule(), "SanitizesInput", "Yes"));
        }

        /// <summary>
        /// Unevidenced output sanitization is reported, and reported as unevidenced.
        /// </summary>
        /// <param name="value">The property value under test, or <see langword="null"/> when absent.</param>
        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("Unknown")]
        public void UnevidencedOutputSanitizationIsReportedTest(string? value)
        {
            AssertUnevidenced(EvaluateProcessToExternal(new UnsanitizedExternalOutputRule(), "SanitizesOutput", value));
        }

        /// <summary>
        /// A stated absence of output sanitization is reported as a confirmed absence.
        /// </summary>
        [TestMethod]
        public void AbsentOutputSanitizationIsReportedAsAbsenceTest()
        {
            AssertConfirmedAbsence(EvaluateProcessToExternal(new UnsanitizedExternalOutputRule(), "SanitizesOutput", "No"));
        }

        /// <summary>
        /// Evidenced output sanitization suppresses the finding.
        /// </summary>
        [TestMethod]
        public void EvidencedOutputSanitizationSuppressesFindingTest()
        {
            AssertNoFinding(EvaluateProcessToExternal(new UnsanitizedExternalOutputRule(), "SanitizesOutput", "Yes"));
        }

        /// <summary>
        /// Unevidenced at-rest encryption is reported, and reported as unevidenced.
        /// </summary>
        /// <param name="value">The property value under test, or <see langword="null"/> when absent.</param>
        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("Unknown")]
        public void UnevidencedEncryptionIsReportedTest(string? value)
        {
            AssertUnevidenced(EvaluateStore(new UnencryptedSecretStoreRule(), "StoresCredentials", "Yes", "Encrypted", value));
        }

        /// <summary>
        /// A stated absence of at-rest encryption is reported as a confirmed absence.
        /// </summary>
        [TestMethod]
        public void AbsentEncryptionIsReportedAsAbsenceTest()
        {
            AssertConfirmedAbsence(EvaluateStore(new UnencryptedSecretStoreRule(), "StoresCredentials", "Yes", "Encrypted", "No"));
        }

        /// <summary>
        /// Every evidenced encryption mechanism suppresses the finding.
        /// </summary>
        /// <param name="value">The property value under test.</param>
        [TestMethod]
        [DataRow("At-rest")]
        [DataRow("TDE")]
        [DataRow("Client-side")]
        [DataRow("Platform")]
        public void EvidencedEncryptionSuppressesFindingTest(string value)
        {
            AssertNoFinding(EvaluateStore(new UnencryptedSecretStoreRule(), "StoresCredentials", "Yes", "Encrypted", value));
        }

        /// <summary>
        /// Unevidenced access control is reported, and reported as unevidenced.
        /// </summary>
        /// <param name="value">The property value under test, or <see langword="null"/> when absent.</param>
        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("Unknown")]
        public void UnevidencedAccessControlIsReportedTest(string? value)
        {
            AssertUnevidenced(EvaluateStore(new UnprotectedCredentialStoreRule(), "StoresCredentials", "Yes", "AccessControl", value));
        }

        /// <summary>
        /// A stated absence of meaningful access control is reported as a confirmed absence.
        /// </summary>
        /// <param name="value">The property value under test.</param>
        [TestMethod]
        [DataRow("None")]
        [DataRow("Public")]
        public void AbsentAccessControlIsReportedAsAbsenceTest(string value)
        {
            AssertConfirmedAbsence(EvaluateStore(new UnprotectedCredentialStoreRule(), "StoresCredentials", "Yes", "AccessControl", value));
        }

        /// <summary>
        /// Every evidenced access control mechanism suppresses the finding.
        /// </summary>
        /// <param name="value">The property value under test.</param>
        [TestMethod]
        [DataRow("RBAC")]
        [DataRow("ACL")]
        public void EvidencedAccessControlSuppressesFindingTest(string value)
        {
            AssertNoFinding(EvaluateStore(new UnprotectedCredentialStoreRule(), "StoresCredentials", "Yes", "AccessControl", value));
        }

        /// <summary>
        /// Unevidenced log signing is reported, and reported as unevidenced.
        /// </summary>
        /// <param name="value">The property value under test, or <see langword="null"/> when absent.</param>
        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("Unknown")]
        public void UnevidencedSigningIsReportedTest(string? value)
        {
            AssertUnevidenced(EvaluateStore(new UnsignedAuditLogStoreRule(), "StoresLogData", "Yes", "Signed", value));
        }

        /// <summary>
        /// A stated absence of log signing is reported as a confirmed absence.
        /// </summary>
        [TestMethod]
        public void AbsentSigningIsReportedAsAbsenceTest()
        {
            AssertConfirmedAbsence(EvaluateStore(new UnsignedAuditLogStoreRule(), "StoresLogData", "Yes", "Signed", "No"));
        }

        /// <summary>
        /// Evidenced log signing suppresses the finding.
        /// </summary>
        [TestMethod]
        public void EvidencedSigningSuppressesFindingTest()
        {
            AssertNoFinding(EvaluateStore(new UnsignedAuditLogStoreRule(), "StoresLogData", "Yes", "Signed", "Yes"));
        }

        /// <summary>
        /// An unevidenced backup is reported, and reported as unevidenced.
        /// </summary>
        /// <param name="value">The property value under test, or <see langword="null"/> when absent.</param>
        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("Unknown")]
        public void UnevidencedBackupIsReportedTest(string? value)
        {
            AssertUnevidenced(EvaluateStore(new DataStoreMissingBackupRule(), "StoresCredentials", "Yes", "Backup", value));
        }

        /// <summary>
        /// A stated absence of backup is reported as a confirmed absence.
        /// </summary>
        [TestMethod]
        public void AbsentBackupIsReportedAsAbsenceTest()
        {
            AssertConfirmedAbsence(EvaluateStore(new DataStoreMissingBackupRule(), "StoresCredentials", "Yes", "Backup", "No"));
        }

        /// <summary>
        /// An evidenced backup suppresses the finding.
        /// </summary>
        [TestMethod]
        public void EvidencedBackupSuppressesFindingTest()
        {
            AssertNoFinding(EvaluateStore(new DataStoreMissingBackupRule(), "StoresCredentials", "Yes", "Backup", "Yes"));
        }

        /// <summary>
        /// An external entity that evidences nothing about its own identity is reported as unevidenced.
        /// </summary>
        /// <param name="value">The property value under test, or <see langword="null"/> when absent.</param>
        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("Unknown")]
        public void UnevidencedExternalAuthenticationIsReportedTest(string? value)
        {
            AssertUnevidenced(EvaluateExternalToProcess(value, null));
        }

        /// <summary>
        /// An external entity that states it does not authenticate is reported as a confirmed absence.
        /// </summary>
        [TestMethod]
        public void AbsentExternalAuthenticationIsReportedAsAbsenceTest()
        {
            AssertConfirmedAbsence(EvaluateExternalToProcess("No", null));
        }

        /// <summary>
        /// An external entity that declares it authenticates itself suppresses the finding.
        /// </summary>
        [TestMethod]
        public void EvidencedExternalAuthenticationSuppressesFindingTest()
        {
            AssertNoFinding(EvaluateExternalToProcess("Yes", null));
        }

        /// <summary>
        /// An evidenced authentication scheme still establishes identity even when AuthenticatesItself
        /// itself carries no evidence.
        /// </summary>
        [TestMethod]
        public void EvidencedExternalSchemeSuppressesFindingWhenSelfDeclarationIsUnknownTest()
        {
            AssertNoFinding(EvaluateExternalToProcess(ControlEvidenceValues.Unknown, "Certificate"));
        }

        /// <summary>
        /// An unevidenced authentication scheme cannot rescue an external entity that also carries no
        /// evidence that it authenticates itself.
        /// </summary>
        [TestMethod]
        public void UnknownExternalSchemeCannotSuppressFindingTest()
        {
            AssertUnevidenced(EvaluateExternalToProcess(ControlEvidenceValues.Unknown, ControlEvidenceValues.Unknown));
        }

        private static void AssertUnevidenced(IList<Message> messages)
        {
            Assert.AreEqual(1, messages.Count, "An unevidenced control must still produce a finding.");
            Assert.IsNotNull(messages[0].Text);
            StringAssert.Contains(
                messages[0].Text!,
                UnevidencedMarker,
                "An unevidenced control must be reported as unevidenced rather than as a confirmed absence.");
        }

        private static void AssertConfirmedAbsence(IList<Message> messages)
        {
            Assert.AreEqual(1, messages.Count, "A stated absence must produce a finding.");
            Assert.IsNotNull(messages[0].Text);
            Assert.IsFalse(
                messages[0].Text!.Contains(UnevidencedMarker),
                "A stated absence must not be reported as merely unevidenced.");
        }

        private static void AssertNoFinding(IList<Message> messages)
        {
            Assert.AreEqual(0, messages.Count, "An evidenced control must suppress the finding.");
        }

        private static IList<Message> Evaluate(Rule rule, ThreatModel model)
        {
            MockMessageWriter writer = new MockMessageWriter();
            using (rule)
            {
                rule.Evaluate(new RuleEvaluationContext(model, writer));
            }

            return writer.Messages;
        }

        private static IList<Message> EvaluateBoundaryProcess(Rule rule, string propertyName, string? value)
        {
            StencilEllipse process = CreateProcess("Order Service", (propertyName, value));
            BorderBoundary border = CreateBoundary();

            // Source sits outside the boundary and the target inside it, so the flow crosses into the
            // more trusted zone and the boundary rules apply.
            Connector inbound = new Connector
            {
                Guid = Guid.NewGuid(),
                SourceX = 0,
                SourceY = 0,
                TargetX = 10,
                TargetY = 11,
                TargetGuid = process.Guid,
            };

            return Evaluate(rule, BuildModel(new Entity[] { process }, new[] { inbound }, border));
        }

        private static IList<Message> EvaluateProcessToExternal(Rule rule, string propertyName, string? value)
        {
            StencilEllipse process = CreateProcess("Order Service", (propertyName, value));
            StencilRectangle external = CreateExternal("Browser");
            Connector outbound = CreateFlow(process, external);

            return Evaluate(rule, BuildModel(new Entity[] { process, external }, new[] { outbound }, null));
        }

        private static IList<Message> EvaluateExternalToProcess(string? authenticatesItself, string? authenticationScheme)
        {
            StencilRectangle external = CreateExternal(
                "Partner",
                ("AuthenticatesItself", authenticatesItself),
                ("AuthenticationScheme", authenticationScheme));
            StencilEllipse process = CreateProcess("Order Service");
            Connector inbound = CreateFlow(external, process);

            return Evaluate(
                new UnauthenticatedExternalSourceRule(),
                BuildModel(new Entity[] { external, process }, new[] { inbound }, null));
        }

        private static IList<Message> EvaluateStore(
            Rule rule,
            string gateName,
            string gateValue,
            string propertyName,
            string? value)
        {
            StencilParallelLines store = CreateStore("Secrets DB", (gateName, gateValue), (propertyName, value));
            return Evaluate(rule, BuildModel(new Entity[] { store }, Array.Empty<Connector>(), null));
        }

        private static StencilEllipse CreateProcess(string name, params (string Name, string? Value)[] properties)
        {
            StencilEllipse process = new StencilEllipse
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = ProcessGenericTypeId,
            };
            AddProperties(process, name, properties);
            return process;
        }

        private static StencilParallelLines CreateStore(string name, params (string Name, string? Value)[] properties)
        {
            StencilParallelLines store = new StencilParallelLines
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = StorageComponentGenericTypeId,
            };
            AddProperties(store, name, properties);
            return store;
        }

        private static StencilRectangle CreateExternal(string name, params (string Name, string? Value)[] properties)
        {
            StencilRectangle external = new StencilRectangle
            {
                Guid = Guid.NewGuid(),
                GenericTypeId = ExternalInteractorGenericTypeId,
            };
            AddProperties(external, name, properties);
            return external;
        }

        private static void AddProperties(Entity entity, string name, (string Name, string? Value)[] properties)
        {
            entity.Properties.Add(new StringDisplayAttribute { DisplayName = "Name", Value = name });
            foreach ((string propertyName, string? propertyValue) in properties)
            {
                // A null value models the property being absent from the model altogether, which is a
                // different input from the property being present and blank.
                if (propertyValue == null)
                {
                    continue;
                }

                entity.Properties.Add(new CustomStringDisplayAttribute { Value = $"{propertyName}:{propertyValue}" });
            }
        }

        private static Connector CreateFlow(Entity source, Entity target)
        {
            return new Connector
            {
                Guid = Guid.NewGuid(),
                SourceGuid = source.Guid,
                TargetGuid = target.Guid,
            };
        }

        private static BorderBoundary CreateBoundary()
        {
            return new BorderBoundary
            {
                Guid = Guid.NewGuid(),
                Left = 5,
                Top = 10,
                Height = 15,
                Width = 20,
            };
        }

        private static ThreatModel BuildModel(Entity[] components, Connector[] connectors, BorderBoundary? boundary)
        {
            DrawingSurfaceModel diagram = new DrawingSurfaceModel { Header = "DFD-0" };
            if (boundary != null)
            {
                diagram.Borders.Add(boundary.Guid, boundary);
            }

            foreach (Entity component in components)
            {
                diagram.Borders.Add(component.Guid, component);
            }

            foreach (Connector connector in connectors)
            {
                diagram.Lines.Add(connector.Guid, connector);
            }

            return new ThreatModel { DrawingSurfaceList = { diagram } };
        }
    }
}
