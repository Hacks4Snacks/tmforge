namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using ThreatModelForge.Model;
    using ThreatModelForge.Model.Abstracts;

    /// <summary>
    /// Evaluates one interaction-v1 expression over connector and synthetic diagram-root contexts.
    /// </summary>
    internal sealed class InteractionRule : Rule
    {
        private const int MaxInteractionsPerRule = 100000;
        private const int MaxOperationsPerRule = 1000000;
        private const int MaxFindingsPerRule = 100000;
        private const long MaxMessageCharactersPerRule = 64L * 1024 * 1024;

        private readonly InteractionExpression expression;
        private readonly string messageTemplate;
        private readonly RulePackDefinition packDefinition;
        private readonly RuleProvenance? provenance;
        private readonly StrideCategory? stride;
        private readonly IReadOnlyList<ThreatReference> threatReferences;
        private readonly IReadOnlyList<PropertyBinding> propertyBindings;
        private readonly Dictionary<string, string?> parents;
        private readonly bool evaluatesRoot;

        /// <summary>Initializes a new instance of the <see cref="InteractionRule"/> class.</summary>
        /// <param name="id">The effective rule identifier.</param>
        /// <param name="severity">The message severity.</param>
        /// <param name="message">The message template.</param>
        /// <param name="fullDescription">The full rule description.</param>
        /// <param name="helpText">The remediation guidance.</param>
        /// <param name="helpUri">The optional help URI.</param>
        /// <param name="stride">The optional STRIDE category.</param>
        /// <param name="threatReferences">The external threat references.</param>
        /// <param name="expression">The compiled interaction expression.</param>
        /// <param name="packDefinition">The containing pack metadata.</param>
        /// <param name="provenance">The optional source provenance.</param>
        internal InteractionRule(
            string id,
            MessageSeverity severity,
            string message,
            string fullDescription,
            string helpText,
            Uri? helpUri,
            StrideCategory? stride,
            IReadOnlyList<ThreatReference> threatReferences,
            InteractionExpression expression,
            RulePackDefinition packDefinition,
            RuleProvenance? provenance)
            : base(id, severity, packDefinition.Id)
        {
            this.messageTemplate = message;
            this.FullDescription = fullDescription;
            this.HelpText = helpText;
            this.HelpUri = helpUri;
            this.stride = stride;
            this.threatReferences = threatReferences;
            this.expression = expression;
            this.evaluatesRoot = UsesRoot(expression);
            this.packDefinition = packDefinition;
            this.provenance = provenance;
            this.parents = packDefinition.ElementTypes.ToDictionary(
                element => element.Id,
                element => element.ParentId,
                StringComparer.OrdinalIgnoreCase);
            this.propertyBindings = BuildPropertyBindings(expression, packDefinition, this.parents);
        }

        /// <inheritdoc/>
        public override StrideCategory? Stride => this.stride;

        /// <inheritdoc/>
        public override IReadOnlyList<ThreatReference> ThreatReferences => this.threatReferences;

        /// <inheritdoc/>
        public override IReadOnlyList<PropertyBinding> PropertyBindings => this.propertyBindings;

        /// <inheritdoc/>
        public override RulePackDefinition PackDefinition => this.packDefinition;

        /// <inheritdoc/>
        public override RuleProvenance? Provenance => this.provenance;

        /// <inheritdoc/>
        public override void Evaluate(RuleEvaluationContext context)
        {
            _ = context ?? throw new ArgumentNullException(nameof(context));
            int interactions = 0;
            int operations = 0;
            int findings = 0;
            long messageCharacters = 0;
            foreach (DrawingSurfaceModel diagram in context.Model.DrawingSurfaceList)
            {
                context.AccountDeclarativeOperations(diagram.Lines.Count);
                if (this.evaluatesRoot)
                {
                    InteractionContext root = InteractionContext.Root(diagram);
                    this.EvaluateContext(
                        context,
                        root,
                        ref interactions,
                        ref operations,
                        ref findings,
                        ref messageCharacters);
                }

                foreach (Connector flow in diagram.Lines.Values.OfType<Connector>())
                {
                    Entity? source = Resolve(diagram, flow.SourceGuid);
                    Entity? target = Resolve(diagram, flow.TargetGuid);
                    InteractionContext interaction = new InteractionContext(
                        diagram,
                        source,
                        target,
                        flow,
                        isRoot: false);
                    this.EvaluateContext(
                        context,
                        interaction,
                        ref interactions,
                        ref operations,
                        ref findings,
                        ref messageCharacters);
                }
            }
        }

        private static Entity? Resolve(DrawingSurfaceModel diagram, Guid id)
        {
            return diagram.Borders.TryGetValue(id, out object? value) ? value as Entity : null;
        }

        private static bool Crosses(Connector flow, Entity boundary)
        {
            return boundary is BorderBoundary border
                ? flow.Crosses(border)
                : boundary is LineBoundary line && flow.Crosses(line);
        }

        private static string? MessageTokenValue(
            string token,
            string sourceName,
            string targetName,
            string flowName)
        {
            if (string.Equals(token, "source", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "source.Name", StringComparison.OrdinalIgnoreCase))
            {
                return sourceName;
            }

            if (string.Equals(token, "target", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "target.Name", StringComparison.OrdinalIgnoreCase))
            {
                return targetName;
            }

            if (string.Equals(token, "flow", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "flow.Name", StringComparison.OrdinalIgnoreCase))
            {
                return flowName;
            }

            return null;
        }

        private static bool UsesRoot(InteractionExpression expression)
        {
            if (expression.Operation == InteractionExpression.OperationKind.Type &&
                string.Equals(expression.Subject, "source", StringComparison.Ordinal) &&
                string.Equals(expression.Type, "ROOT", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return (expression.Child != null && UsesRoot(expression.Child)) || expression.Children.Any(UsesRoot);
        }

        private static IReadOnlyList<PropertyBinding> BuildPropertyBindings(
            InteractionExpression expression,
            RulePackDefinition pack,
            IReadOnlyDictionary<string, string?> parents)
        {
            List<PropertyBinding> bindings = new List<PropertyBinding>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddPropertyBindings(expression, pack, parents, bindings, seen);
            return bindings.AsReadOnly();
        }

        private static void AddPropertyBindings(
            InteractionExpression expression,
            RulePackDefinition pack,
            IReadOnlyDictionary<string, string?> parents,
            List<PropertyBinding> bindings,
            ISet<string> seen)
        {
            if (expression.Operation == InteractionExpression.OperationKind.Property)
            {
                foreach (string appliesTo in PropertyKinds(expression, pack, parents))
                {
                    string key = appliesTo + "\u0000" + expression.Property;
                    if (seen.Add(key))
                    {
                        bindings.Add(new PropertyBinding(appliesTo, expression.Property!));
                    }
                }
            }

            if (expression.Child != null)
            {
                AddPropertyBindings(expression.Child, pack, parents, bindings, seen);
            }

            foreach (InteractionExpression child in expression.Children)
            {
                AddPropertyBindings(child, pack, parents, bindings, seen);
            }
        }

        private static IEnumerable<string> PropertyKinds(
            InteractionExpression expression,
            RulePackDefinition pack,
            IReadOnlyDictionary<string, string?> parents)
        {
            if (string.Equals(expression.Subject, "flow", StringComparison.Ordinal))
            {
                return new[] { "flow" };
            }

            RulePropertyDefinition? definition = pack.ResolveProperty(expression.Property!);
            List<string> kinds = (definition?.ElementTypeIds ?? Array.Empty<string>())
                .Select(type => PrimitiveKind(type, parents))
                .Where(kind => kind != null)
                .Select(kind => kind!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return kinds.Count > 0 ? kinds : new[] { "process", "datastore", "external" };
        }

        private static string? PrimitiveKind(
            string type,
            IReadOnlyDictionary<string, string?> parents)
        {
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? current = type;
            while (!string.IsNullOrWhiteSpace(current) && visited.Add(current!))
            {
                if (string.Equals(current, "GE.P", StringComparison.OrdinalIgnoreCase))
                {
                    return "process";
                }

                if (string.Equals(current, "GE.DS", StringComparison.OrdinalIgnoreCase))
                {
                    return "datastore";
                }

                if (string.Equals(current, "GE.EI", StringComparison.OrdinalIgnoreCase))
                {
                    return "external";
                }

                current = parents.TryGetValue(current!, out string? parent) ? parent : null;
            }

            return null;
        }

        private void EvaluateContext(
            RuleEvaluationContext context,
            InteractionContext interaction,
            ref int interactions,
            ref int operations,
            ref int findings,
            ref long messageCharacters)
        {
            interactions++;
            if (interactions > MaxInteractionsPerRule)
            {
                throw new InvalidDataException($"Rule '{this.ID}' exceeded the interaction limit of {MaxInteractionsPerRule}.");
            }

            if (!this.EvaluateExpression(this.expression, interaction, context, ref operations))
            {
                return;
            }

            findings++;
            if (findings > MaxFindingsPerRule)
            {
                throw new InvalidDataException($"Rule '{this.ID}' exceeded the finding limit of {MaxFindingsPerRule}.");
            }

            context.AccountDeclarativeOperations(interaction.Source?.Properties.Count ?? 0);
            context.AccountDeclarativeOperations(interaction.Target?.Properties.Count ?? 0);
            context.AccountDeclarativeOperations(interaction.Flow?.Properties.Count ?? 0);
            string sourceName = interaction.Source?.Name() ?? string.Empty;
            string targetName = interaction.Target?.Name() ?? string.Empty;
            string flowName = interaction.Flow?.Name() ?? string.Empty;
            string message = ExpandMessageTemplate(
                this.messageTemplate,
                token => MessageTokenValue(token, sourceName, targetName, flowName));
            messageCharacters += message.Length;
            if (messageCharacters > MaxMessageCharactersPerRule)
            {
                throw new InvalidDataException(
                    $"Rule '{this.ID}' exceeded the message-output limit of {MaxMessageCharactersPerRule} characters.");
            }

            Entity target = interaction.IsRoot ? interaction.Diagram : interaction.Flow!;
            context.Writer.Write(this.CreateMessage(target, interaction.Diagram, message));
        }

        private bool EvaluateExpression(
            InteractionExpression node,
            InteractionContext interaction,
            RuleEvaluationContext context,
            ref int operations)
        {
            this.AccountOperation(context, ref operations);

            switch (node.Operation)
            {
                case InteractionExpression.OperationKind.All:
                    foreach (InteractionExpression child in node.Children)
                    {
                        if (!this.EvaluateExpression(child, interaction, context, ref operations))
                        {
                            return false;
                        }
                    }

                    return true;
                case InteractionExpression.OperationKind.Any:
                    foreach (InteractionExpression child in node.Children)
                    {
                        if (this.EvaluateExpression(child, interaction, context, ref operations))
                        {
                            return true;
                        }
                    }

                    return false;
                case InteractionExpression.OperationKind.Not:
                    return !this.EvaluateExpression(node.Child!, interaction, context, ref operations);
                case InteractionExpression.OperationKind.Crosses:
                    if (interaction.Flow == null)
                    {
                        return false;
                    }

                    context.AccountDeclarativeOperations(interaction.Diagram.Borders.Count);
                    context.AccountDeclarativeOperations(interaction.Diagram.Lines.Count);
                    foreach (Entity boundary in interaction.BoundaryCandidates())
                    {
                        operations++;
                        if (operations > MaxOperationsPerRule)
                        {
                            throw new InvalidDataException(
                                $"Rule '{this.ID}' exceeded the operation limit of {MaxOperationsPerRule}.");
                        }

                        if (Crosses(interaction.Flow!, boundary) &&
                            this.MatchesType(boundary, node.BoundaryType!, context, ref operations))
                        {
                            return true;
                        }
                    }

                    return false;
                case InteractionExpression.OperationKind.Type:
                    if (string.Equals(node.Subject, "source", StringComparison.Ordinal) &&
                        string.Equals(node.Type, "ROOT", StringComparison.OrdinalIgnoreCase))
                    {
                        return interaction.IsRoot;
                    }

                    return this.MatchesType(interaction.Subject(node.Subject!), node.Type!, context, ref operations);
                case InteractionExpression.OperationKind.Property:
                    Entity? subject = interaction.Subject(node.Subject!);
                    if (subject == null)
                    {
                        return false;
                    }

                    this.AccountOperations(context, ref operations, subject.Properties.Count);
                    this.AccountOperations(context, ref operations, subject.Properties.Count);
                    if (!subject.TryGetCustomPropertyValues(node.Property!, out IList<string> values))
                    {
                        return false;
                    }

                    foreach (string value in values)
                    {
                        foreach (string expected in node.Values)
                        {
                            this.AccountOperation(context, ref operations);
                            if (string.Equals(value, expected, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                default:
                    return false;
            }
        }

        private bool MatchesType(
            Entity? entity,
            string expected,
            RuleEvaluationContext context,
            ref int operations)
        {
            if (entity == null)
            {
                return false;
            }

            return this.MatchesTypeId(entity.TypeId, expected, context, ref operations) ||
                this.MatchesTypeId(entity.GenericTypeId, expected, context, ref operations);
        }

        private bool MatchesTypeId(
            string? actual,
            string expected,
            RuleEvaluationContext context,
            ref int operations)
        {
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (!string.IsNullOrWhiteSpace(actual) && visited.Add(actual!))
            {
                this.AccountOperation(context, ref operations);
                if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                actual = this.parents.TryGetValue(actual!, out string? parent) ? parent : null;
            }

            return false;
        }

        private void AccountOperation(RuleEvaluationContext context, ref int operations)
        {
            this.AccountOperations(context, ref operations, count: 1);
        }

        private void AccountOperations(RuleEvaluationContext context, ref int operations, int count)
        {
            context.AccountDeclarativeOperations(count);
            if (count < 0 || operations > MaxOperationsPerRule - count)
            {
                throw new InvalidDataException(
                    $"Rule '{this.ID}' exceeded the operation limit of {MaxOperationsPerRule}.");
            }

            operations += count;
        }

        private sealed class InteractionContext
        {
            public InteractionContext(
                DrawingSurfaceModel diagram,
                Entity? source,
                Entity? target,
                Connector? flow,
                bool isRoot)
            {
                this.Diagram = diagram;
                this.Source = source;
                this.Target = target;
                this.Flow = flow;
                this.IsRoot = isRoot;
            }

            public DrawingSurfaceModel Diagram { get; }

            public Entity? Source { get; }

            public Entity? Target { get; }

            public Connector? Flow { get; }

            public bool IsRoot { get; }

            public static InteractionContext Root(DrawingSurfaceModel diagram) =>
                new InteractionContext(diagram, null, null, null, isRoot: true);

            public IEnumerable<Entity> BoundaryCandidates()
            {
                return this.Diagram.TrustBoundaryBorders().OfType<Entity>()
                    .Concat(this.Diagram.TrustBoundaryLines().OfType<Entity>());
            }

            public Entity? Subject(string subject)
            {
                if (string.Equals(subject, "source", StringComparison.Ordinal))
                {
                    return this.Source;
                }

                if (string.Equals(subject, "target", StringComparison.Ordinal))
                {
                    return this.Target;
                }

                return string.Equals(subject, "flow", StringComparison.Ordinal) ? this.Flow : null;
            }
        }
    }
}
