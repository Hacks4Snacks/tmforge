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
        private readonly InteractionExpression expression;
        private readonly InteractionExpression.Evaluator evaluator;
        private readonly RulePackDefinition? packDefinition;
        private readonly RuleProvenance? provenance;

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
        /// <param name="packDefinition">The validated v2 pack definition, or <see langword="null"/>.</param>
        /// <param name="provenance">The original source identity and expressions, or <see langword="null"/>.</param>
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
            DeclarativeCondition? assert,
            RulePackDefinition? packDefinition,
            RuleProvenance? provenance)
            : base(id, severity, pack)
        {
            this.appliesTo = appliesTo;
            this.messageTemplate = messageTemplate;
            this.FullDescription = fullDescription;
            this.HelpText = helpText;
            this.HelpUri = helpUri;
            this.stride = stride;
            this.threatReferences = threatReferences;
            this.expression = CompileFindingExpression(appliesTo, when, assert);
            this.evaluator = new InteractionExpression.Evaluator(
                id,
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
                operationLimit: null,
                accountExpressionNodes: false);
            this.packDefinition = packDefinition;
            this.provenance = provenance;
            this.propertyBindings = BuildBindings(appliesTo, when, assert);
        }

        /// <inheritdoc/>
        public override StrideCategory? Stride => this.stride;

        /// <inheritdoc/>
        public override IReadOnlyList<ThreatReference> ThreatReferences => this.threatReferences;

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => this.propertyBindings;

        /// <inheritdoc/>
        public override RulePackDefinition? PackDefinition => this.packDefinition;

        /// <inheritdoc/>
        public override RuleProvenance? Provenance => this.provenance;

        /// <summary>Gets the immutable expression compiled from the flat guard and requirement.</summary>
        internal InteractionExpression CompiledExpression => this.expression;

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));
            int operations = 0;

            foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
            {
                foreach (Entity element in this.Candidates(diagram, context))
                {
                    context.AccountDeclarativeOperations();
                    InteractionExpression.EvaluationContext evaluationContext = CreateEvaluationContext(diagram, element);
                    if (!this.evaluator.Evaluate(this.expression, evaluationContext, context, ref operations))
                    {
                        continue;
                    }

                    context.AccountDeclarativeOperations(element.Properties.Count);
                    context.AccountDeclarativeOperations(element.Properties.Count);
                    string name = GetEntityDisplayText(element);
                    string text = ExpandMessageTemplate(
                        this.messageTemplate,
                        token => string.Equals(token, "name", StringComparison.Ordinal) ? name : null);
                    context.Writer.Write(this.CreateMessage(element, diagram, text));
                }
            }
        }

        private static InteractionExpression.EvaluationContext CreateEvaluationContext(
            DrawingSurfaceModel diagram,
            Entity element)
        {
            if (element is Connector flow)
            {
                Entity? source = diagram.Borders.TryGetValue(flow.SourceGuid, out object? sourceValue)
                    ? sourceValue as Entity
                    : null;
                Entity? target = diagram.Borders.TryGetValue(flow.TargetGuid, out object? targetValue)
                    ? targetValue as Entity
                    : null;
                return new InteractionExpression.EvaluationContext(diagram, source, target, flow, isRoot: false);
            }

            return new InteractionExpression.EvaluationContext(diagram, element, null, null, isRoot: false);
        }

        private static InteractionExpression CompileFindingExpression(
            string appliesTo,
            DeclarativeCondition? when,
            DeclarativeCondition? assert)
        {
            string candidateSubject = string.Equals(appliesTo, "flow", StringComparison.OrdinalIgnoreCase)
                ? "flow"
                : "source";
            List<InteractionExpression> expressions = new List<InteractionExpression>();
            if (when != null)
            {
                expressions.Add(CompileCondition(when, candidateSubject));
            }

            if (assert != null)
            {
                expressions.Add(InteractionExpression.Negate(CompileCondition(assert, candidateSubject)));
            }

            return Conjunction(expressions, candidateSubject);
        }

        private static InteractionExpression CompileCondition(
            DeclarativeCondition condition,
            string candidateSubject)
        {
            List<InteractionExpression> expressions = new List<InteractionExpression>();
            if (condition.Property != null)
            {
                AddPropertyExpressions(
                    expressions,
                    candidateSubject,
                    condition.Property,
                    condition.AnyOf,
                    condition.NotAnyOf,
                    condition.EqualTo,
                    condition.Present);
            }

            if (condition.CrossesTrustBoundary.HasValue)
            {
                InteractionExpression crosses = InteractionExpression.CrossesAnyBoundary();
                expressions.Add(condition.CrossesTrustBoundary.Value
                    ? crosses
                    : InteractionExpression.Negate(crosses));
            }

            if (condition.Source != null)
            {
                expressions.Add(CompileEndpoint(condition.Source, "source"));
            }

            if (condition.Target != null)
            {
                expressions.Add(CompileEndpoint(condition.Target, "target"));
            }

            return Conjunction(expressions, candidateSubject);
        }

        private static InteractionExpression CompileEndpoint(
            DeclarativeEndpoint endpoint,
            string subject)
        {
            List<InteractionExpression> expressions = new List<InteractionExpression>
            {
                InteractionExpression.SubjectExists(subject),
            };

            if (endpoint.Kind != null)
            {
                expressions.Add(InteractionExpression.KindIs(subject, endpoint.Kind));
            }

            if (endpoint.Property != null)
            {
                AddPropertyExpressions(
                    expressions,
                    subject,
                    endpoint.Property,
                    endpoint.AnyOf,
                    endpoint.NotAnyOf,
                    endpoint.EqualTo,
                    endpoint.Present);
            }

            return Conjunction(expressions, subject);
        }

        private static void AddPropertyExpressions(
            ICollection<InteractionExpression> expressions,
            string subject,
            string property,
            IReadOnlyList<string>? anyOf,
            IReadOnlyList<string>? notAnyOf,
            string? equalTo,
            bool? present)
        {
            expressions.Add(InteractionExpression.FlatPropertyCondition(
                subject,
                property,
                anyOf,
                notAnyOf,
                equalTo,
                present));
        }

        private static InteractionExpression Conjunction(
            IReadOnlyList<InteractionExpression> expressions,
            string candidateSubject)
        {
            if (expressions.Count == 0)
            {
                return InteractionExpression.SubjectExists(candidateSubject);
            }

            return expressions.Count == 1
                ? expressions[0]
                : InteractionExpression.All(expressions);
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

        private IEnumerable<Entity> Candidates(DrawingSurfaceModel diagram, RuleEvaluationContext context)
        {
            if (string.Equals(this.appliesTo, "flow", StringComparison.OrdinalIgnoreCase))
            {
                context.AccountDeclarativeOperations(diagram.Lines.Count);
                return diagram.Lines.Values.OfType<Connector>().ToList();
            }

            context.AccountDeclarativeOperations(diagram.Borders.Count);
            return diagram.Components()
                .Where(entity => InteractionExpression.MatchesPrimitiveKind(entity, this.appliesTo))
                .ToList();
        }
    }
}
