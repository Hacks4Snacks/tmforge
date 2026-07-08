namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Collections.Generic;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Rule that flags a flow that caches a credential read from a credential store, which can serve
    /// a stale credential after the credential is rotated or revoked.
    /// </summary>
    public class CachedCredentialReadRule : Rule
    {
        /// <summary>
        /// Custom attribute that marks a flow as caching the value it reads.
        /// </summary>
        public const string CachedCustomAttributeName = "Cached";

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedCredentialReadRule"/> class.
        /// </summary>
        public CachedCredentialReadRule()
            : base(RuleIDs.CachedCredentialReadRule, MessageSeverity.Warning, RulePackCatalog.DataProtection)
        {
            this.FullDescription = CachedCredentialReadRuleResources.FullDescription;
            this.HelpText = CachedCredentialReadRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => new[]
        {
            new PropertyBinding("flow", CachedCustomAttributeName, "Yes"),
        };

        /// <inheritdoc/>
        public override StrideCategory? Stride => StrideCategory.InformationDisclosure;

        /// <inheritdoc/>
        public override IReadOnlyList<ThreatReference> ThreatReferences => new[]
        {
            ThreatReference.Cwe(522),
        };

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));

            foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
            {
                foreach (Connector connector in diagram.Lines.Values.OfType<Connector>())
                {
                    if (!IsCached(connector))
                    {
                        continue;
                    }

                    if (TryGetCredentialStore(diagram, connector, out Entity? store))
                    {
                        string text = string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            CachedCredentialReadRuleResources.MessageText,
                            GetEntityDisplayText(connector),
                            GetEntityDisplayText(store!));
                        context.Writer.Write(this.CreateMessage(connector, diagram, text));
                    }
                }
            }
        }

        private static bool IsCached(Connector connector)
        {
            return connector.TryGetCustomPropertyValue(CachedCustomAttributeName, out string? value) &&
                string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetCredentialStore(DrawingSurfaceModel diagram, Connector connector, out Entity? store)
        {
            return IsCredentialStore(diagram, connector.SourceGuid, out store) ||
                IsCredentialStore(diagram, connector.TargetGuid, out store);
        }

        private static bool IsCredentialStore(DrawingSurfaceModel diagram, Guid guid, out Entity? store)
        {
            store = null;
            if (diagram.Borders.TryGetValue(guid, out object? value) &&
                value is Entity entity &&
                entity.IsStorageComponent() &&
                entity.TryGetCustomPropertyValue("StoresCredentials", out string? stores) &&
                string.Equals(stores, "Yes", StringComparison.OrdinalIgnoreCase))
            {
                store = entity;
                return true;
            }

            return false;
        }
    }
}
