namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Rule that flags a data store holding important data (credentials or audit/log data) that declares
    /// no backup, so destruction or corruption of the store's data is unrecoverable.
    /// </summary>
    /// <remarks>
    /// A data store flagged as holding credentials (<c>StoresCredentials = Yes</c>) or log/audit data
    /// (<c>StoresLogData = Yes</c>) whose <c>Backup</c> property is <c>No</c> or unset has no recovery path:
    /// accidental deletion, corruption, or a destructive attack (for example ransomware) permanently loses
    /// the secrets an application depends on or the audit trail that proves what happened — a loss of
    /// availability, and for audit data a loss of the evidence needed for non-repudiation.
    /// </remarks>
    public class DataStoreMissingBackupRule : Rule
    {
        /// <summary>
        /// Custom attribute that records whether a store is backed up.
        /// </summary>
        public const string BackupCustomAttributeName = "Backup";

        /// <summary>
        /// Initializes a new instance of the <see cref="DataStoreMissingBackupRule"/> class.
        /// </summary>
        public DataStoreMissingBackupRule()
            : base(RuleIDs.DataStoreMissingBackupRule, MessageSeverity.Warning, RulePackCatalog.Availability)
        {
            this.FullDescription = DataStoreMissingBackupRuleResources.FullDescription;
            this.HelpText = DataStoreMissingBackupRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => new[]
        {
            new PropertyBinding("datastore", BackupCustomAttributeName, "No"),
            new PropertyBinding("datastore", "StoresCredentials"),
            new PropertyBinding("datastore", "StoresLogData"),
        };

        /// <inheritdoc/>
        public override StrideCategory? Stride => StrideCategory.DenialOfService;

        /// <inheritdoc/>
        public override IReadOnlyList<ThreatReference> ThreatReferences => new[]
        {
            ThreatReference.Attack("T1485"),
            ThreatReference.Attack("T1490"),
        };

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));

            foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
            {
                foreach (Entity component in diagram.Components())
                {
                    if (!component.IsStorageComponent())
                    {
                        continue;
                    }

                    if (!HoldsImportantData(component) || IsBackedUp(component))
                    {
                        continue;
                    }

                    string text = string.Format(
                        System.Globalization.CultureInfo.CurrentCulture,
                        DataStoreMissingBackupRuleResources.MessageText,
                        GetEntityDisplayText(component));
                    context.Writer.Write(this.CreateMessage(component, diagram, text));
                }
            }
        }

        private static bool HoldsImportantData(Entity component)
        {
            return IsYes(component, "StoresCredentials") || IsYes(component, "StoresLogData");
        }

        private static bool IsBackedUp(Entity component)
        {
            return IsYes(component, BackupCustomAttributeName);
        }

        private static bool IsYes(Entity component, string propertyName)
        {
            return component.TryGetCustomPropertyValue(propertyName, out string? value) &&
                string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
