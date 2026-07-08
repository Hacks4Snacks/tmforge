namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Collections.Generic;
    using ThreatModelForge.Model;

    /// <summary>
    /// Rule that flags a single static identity asserted by flows from multiple distinct sources,
    /// which yields an over-broad, shared principal.
    /// </summary>
    public class SharedStaticIdentityRule : Rule
    {
        /// <summary>
        /// Custom attribute that names the identity (principal / subject) a flow asserts.
        /// </summary>
        public const string IdentityCustomAttributeName = "Identity";

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedStaticIdentityRule"/> class.
        /// </summary>
        public SharedStaticIdentityRule()
            : base(RuleIDs.SharedStaticIdentityRule, MessageSeverity.Warning, RulePackCatalog.IdentityAccess)
        {
            this.FullDescription = SharedStaticIdentityRuleResources.FullDescription;
            this.HelpText = SharedStaticIdentityRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => new[]
        {
            new PropertyBinding("flow", IdentityCustomAttributeName),
        };

        /// <inheritdoc/>
        public override StrideCategory? Stride => StrideCategory.Spoofing;

        /// <inheritdoc/>
        public override IReadOnlyList<ThreatReference> ThreatReferences => new[]
        {
            ThreatReference.Cwe(287),
        };

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));

            foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
            {
                Dictionary<string, List<Connector>> byIdentity = new Dictionary<string, List<Connector>>(StringComparer.OrdinalIgnoreCase);
                foreach (Connector connector in diagram.Lines.Values.OfType<Connector>())
                {
                    if (!connector.TryGetCustomPropertyValue(IdentityCustomAttributeName, out string? identity) ||
                        string.IsNullOrWhiteSpace(identity))
                    {
                        continue;
                    }

                    string key = identity!.Trim();
                    if (!byIdentity.TryGetValue(key, out List<Connector>? connectors))
                    {
                        connectors = new List<Connector>();
                        byIdentity[key] = connectors;
                    }

                    connectors.Add(connector);
                }

                foreach (KeyValuePair<string, List<Connector>> pair in byIdentity)
                {
                    HashSet<Guid> sources = new HashSet<Guid>();
                    foreach (Connector connector in pair.Value)
                    {
                        sources.Add(connector.SourceGuid);
                    }

                    if (sources.Count < 2)
                    {
                        continue;
                    }

                    foreach (Connector connector in pair.Value)
                    {
                        string text = string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            SharedStaticIdentityRuleResources.MessageText,
                            GetEntityDisplayText(connector),
                            pair.Key,
                            sources.Count);
                        context.Writer.Write(this.CreateMessage(connector, diagram, text));
                    }
                }
            }
        }
    }
}
