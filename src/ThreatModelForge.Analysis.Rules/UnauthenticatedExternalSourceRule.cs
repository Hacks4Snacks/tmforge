namespace ThreatModelForge.Analysis.Rules
{
    using System;
    using System.Linq;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Rule that checks that an external entity initiating flows into the system authenticates itself.
    /// </summary>
    /// <remarks>
    /// An external interactor that sends a data flow into the system but does not authenticate itself
    /// (<c>AuthenticatesItself = No</c> or unset, and no <c>AuthenticationScheme</c>) cannot be reliably
    /// identified by the elements it calls, so an attacker can spoof its identity and act on its behalf.
    /// Setting <c>AuthenticatesItself = Yes</c> or a non-<c>None</c> <c>AuthenticationScheme</c> (for
    /// example a token or an SSH public key) records how the entity proves its identity and clears the
    /// finding.
    /// </remarks>
    public class UnauthenticatedExternalSourceRule : Rule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnauthenticatedExternalSourceRule"/> class.
        /// </summary>
        public UnauthenticatedExternalSourceRule()
            : base(RuleIDs.UnauthenticatedExternalSourceRule, MessageSeverity.Warning, RulePackCatalog.IdentityAccess)
        {
            this.FullDescription = UnauthenticatedExternalSourceRuleResources.FullDescription;
            this.HelpText = UnauthenticatedExternalSourceRuleResources.HelpText;
            this.HelpUri = RuleDocumentation.HelpUriFor(this.ID);
        }

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => new[]
        {
            new PropertyBinding("external", "AuthenticatesItself", "No"),
            new PropertyBinding("external", "AuthenticationScheme", "None"),
        };

        /// <inheritdoc/>
        public override StrideCategory? Stride => StrideCategory.Spoofing;

        /// <inheritdoc/>
        public override IReadOnlyList<ThreatReference> ThreatReferences => new[]
        {
            ThreatReference.Cwe(287),
            ThreatReference.Capec(151),
        };

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));

            foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
            {
                foreach (Entity component in diagram.Components())
                {
                    if (!component.IsExternalInteractor())
                    {
                        continue;
                    }

                    if (AuthenticatesItself(component))
                    {
                        continue;
                    }

                    if (InitiatesFlowIntoSystem(diagram, component))
                    {
                        string text = string.Format(
                            System.Globalization.CultureInfo.CurrentCulture,
                            UnauthenticatedExternalSourceRuleResources.MessageText,
                            GetEntityDisplayText(component));
                        context.Writer.Write(this.CreateMessage(component, diagram, text));
                    }
                }
            }
        }

        private static bool AuthenticatesItself(Entity component)
        {
            if (component.TryGetCustomPropertyValue("AuthenticatesItself", out string? value) &&
                string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // A declared authentication scheme (for example an ARM RP token or an operator's SSH public
            // key) also establishes the external's identity, so treat any scheme other than "None" as
            // authenticating.
            return component.TryGetCustomPropertyValue("AuthenticationScheme", out string? scheme) &&
                !string.IsNullOrWhiteSpace(scheme) &&
                !string.Equals(scheme, "None", StringComparison.OrdinalIgnoreCase);
        }

        private static bool InitiatesFlowIntoSystem(DrawingSurfaceModel diagram, Entity external)
        {
            foreach (Connector connector in diagram.Lines.Values.OfType<Connector>())
            {
                if (connector.SourceGuid != external.Guid)
                {
                    continue;
                }

                if (diagram.Borders.TryGetValue(connector.TargetGuid, out object? target) &&
                    target is Entity entity &&
                    !entity.IsExternalInteractor())
                {
                    return true;
                }
            }

            return false;
        }
    }
}
