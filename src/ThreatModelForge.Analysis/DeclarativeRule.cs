namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// A <see cref="Rule"/> whose behavior is defined by data (a <see cref="DeclarativeRuleSpec"/>)
    /// rather than compiled code. It evaluates a guard/requirement pair over the elements of a single
    /// DFD primitive, reusing the same model helpers (<see cref="Extensions"/>) the built-in rules use,
    /// so a declarative rule resolves trust-boundary crossings and endpoint kinds identically to a
    /// first-party rule. A finding is raised for each candidate that matches the guard and fails the
    /// requirement.
    /// </summary>
    internal sealed class DeclarativeRule : Rule
    {
        private readonly string appliesTo;
        private readonly string messageTemplate;
        private readonly StrideCategory? stride;
        private readonly IReadOnlyList<ThreatReference> threatReferences;
        private readonly IReadOnlyList<PropertyBinding> propertyBindings;
        private readonly DeclarativeCondition? whenCondition;
        private readonly DeclarativeCondition? assertCondition;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeclarativeRule"/> class.
        /// </summary>
        /// <param name="id">The rule id.</param>
        /// <param name="pack">The rule pack id.</param>
        /// <param name="severity">The rule severity.</param>
        /// <param name="appliesTo">The DFD primitive the rule targets.</param>
        /// <param name="messageTemplate">The finding message template (<c>{name}</c> is substituted).</param>
        /// <param name="fullDescription">The long description.</param>
        /// <param name="helpText">The remediation guidance.</param>
        /// <param name="helpUri">The optional documentation URL.</param>
        /// <param name="stride">The optional STRIDE category.</param>
        /// <param name="threatReferences">The external references.</param>
        /// <param name="when">The optional guard condition.</param>
        /// <param name="assert">The optional requirement condition.</param>
        internal DeclarativeRule(
            string id,
            string pack,
            MessageSeverity severity,
            string appliesTo,
            string messageTemplate,
            string fullDescription,
            string helpText,
            Uri? helpUri,
            StrideCategory? stride,
            IReadOnlyList<ThreatReference> threatReferences,
            DeclarativeCondition? when,
            DeclarativeCondition? assert)
            : base(id, severity, pack)
        {
            this.appliesTo = appliesTo;
            this.messageTemplate = messageTemplate;
            this.FullDescription = fullDescription;
            this.HelpText = helpText;
            this.HelpUri = helpUri;
            this.stride = stride;
            this.threatReferences = threatReferences;
            this.whenCondition = when;
            this.assertCondition = assert;
            this.propertyBindings = BuildBindings(appliesTo, when, assert);
        }

        /// <inheritdoc/>
        public override StrideCategory? Stride => this.stride;

        /// <inheritdoc/>
        public override IReadOnlyList<ThreatReference> ThreatReferences => this.threatReferences;

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => this.propertyBindings;

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));

            foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
            {
                foreach (Entity element in this.Candidates(diagram))
                {
                    if (this.whenCondition != null && !EvaluateCondition(this.whenCondition, diagram, element))
                    {
                        continue;
                    }

                    // A finding is raised when the guard matches and the requirement is not satisfied.
                    // A rule with no requirement flags every guard match unconditionally.
                    if (this.assertCondition != null && EvaluateCondition(this.assertCondition, diagram, element))
                    {
                        continue;
                    }

                    string text = this.messageTemplate.Replace("{name}", GetEntityDisplayText(element));
                    context.Writer.Write(this.CreateMessage(element, diagram, text));
                }
            }
        }

        private static bool MatchesKind(Entity entity, string kind)
        {
            if (string.Equals(kind, "external", StringComparison.OrdinalIgnoreCase))
            {
                return entity.IsExternalInteractor();
            }

            if (string.Equals(kind, "datastore", StringComparison.OrdinalIgnoreCase))
            {
                return entity.IsStorageComponent();
            }

            if (string.Equals(kind, "process", StringComparison.OrdinalIgnoreCase))
            {
                return entity.IsComponent() && !entity.IsExternalInteractor() && !entity.IsStorageComponent();
            }

            return false;
        }

        private static bool EvaluateCondition(DeclarativeCondition condition, DrawingSurfaceModel diagram, Entity element)
        {
            if (condition.Property != null &&
                !MatchProperty(element, condition.Property, condition.AnyOf, condition.NotAnyOf, condition.EqualTo, condition.Present))
            {
                return false;
            }

            if (condition.CrossesTrustBoundary.HasValue)
            {
                bool crosses = element is Connector connector && diagram.TrustBoundaryCrossings(connector).Any();
                if (crosses != condition.CrossesTrustBoundary.Value)
                {
                    return false;
                }
            }

            if (condition.Source != null && !MatchEndpoint(diagram, element, condition.Source, isSource: true))
            {
                return false;
            }

            if (condition.Target != null && !MatchEndpoint(diagram, element, condition.Target, isSource: false))
            {
                return false;
            }

            return true;
        }

        private static bool MatchEndpoint(DrawingSurfaceModel diagram, Entity element, DeclarativeEndpoint endpoint, bool isSource)
        {
            if (element is not Connector connector)
            {
                return false;
            }

            Guid guid = isSource ? connector.SourceGuid : connector.TargetGuid;
            if (!diagram.Borders.TryGetValue(guid, out object? value) || value is not Entity endpointEntity)
            {
                return false;
            }

            if (endpoint.Kind != null && !MatchesKind(endpointEntity, endpoint.Kind))
            {
                return false;
            }

            if (endpoint.Property != null &&
                !MatchProperty(endpointEntity, endpoint.Property, endpoint.AnyOf, endpoint.NotAnyOf, endpoint.EqualTo, endpoint.Present))
            {
                return false;
            }

            return true;
        }

        private static bool MatchProperty(
            Entity entity,
            string property,
            IReadOnlyList<string>? anyOf,
            IReadOnlyList<string>? notAnyOf,
            string? equalTo,
            bool? present)
        {
            bool defined = entity.TryGetCustomPropertyValue(property, out string? raw);
            string value = raw ?? string.Empty;
            bool isPresent = defined && !string.IsNullOrWhiteSpace(value);

            bool hasMatcher = present.HasValue ||
                equalTo != null ||
                (anyOf != null && anyOf.Count > 0) ||
                (notAnyOf != null && notAnyOf.Count > 0);

            // A bare property with no value matcher means "the property must be present".
            if (!hasMatcher)
            {
                return isPresent;
            }

            if (present.HasValue && isPresent != present.Value)
            {
                return false;
            }

            if (equalTo != null && (!isPresent || !string.Equals(value, equalTo, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            if (anyOf != null && anyOf.Count > 0 && (!isPresent || !anyOf.Contains(value, StringComparer.OrdinalIgnoreCase)))
            {
                return false;
            }

            if (notAnyOf != null && notAnyOf.Count > 0 && isPresent && notAnyOf.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static IReadOnlyList<PropertyBinding> BuildBindings(string appliesTo, DeclarativeCondition? when, DeclarativeCondition? assert)
        {
            List<PropertyBinding> bindings = new List<PropertyBinding>();
            AddBinding(bindings, appliesTo, when);
            AddBinding(bindings, appliesTo, assert);
            return bindings;
        }

        private static void AddBinding(List<PropertyBinding> bindings, string appliesTo, DeclarativeCondition? condition)
        {
            if (condition == null)
            {
                return;
            }

            if (condition.Property != null)
            {
                string[] flagged = (condition.NotAnyOf ?? new List<string>()).ToArray();
                bindings.Add(new PropertyBinding(appliesTo, condition.Property, flagged));
            }

            AddEndpointBinding(bindings, condition.Source);
            AddEndpointBinding(bindings, condition.Target);
        }

        private static void AddEndpointBinding(List<PropertyBinding> bindings, DeclarativeEndpoint? endpoint)
        {
            if (endpoint?.Property == null || endpoint.Kind == null)
            {
                return;
            }

            string[] flagged = (endpoint.NotAnyOf ?? new List<string>()).ToArray();
            bindings.Add(new PropertyBinding(endpoint.Kind, endpoint.Property, flagged));
        }

        private IEnumerable<Entity> Candidates(DrawingSurfaceModel diagram)
        {
            if (string.Equals(this.appliesTo, "flow", StringComparison.OrdinalIgnoreCase))
            {
                return diagram.Lines.Values.OfType<Connector>();
            }

            return diagram.Components().Where(entity => MatchesKind(entity, this.appliesTo));
        }
    }
}
