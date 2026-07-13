namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// An immutable node in the interaction-v1 expression tree.
    /// </summary>
    internal sealed class InteractionExpression
    {
        private InteractionExpression(
            OperationKind operation,
            IReadOnlyList<InteractionExpression>? children = null,
            InteractionExpression? child = null,
            string? subject = null,
            string? type = null,
            string? property = null,
            IReadOnlyList<string>? values = null,
            string? boundaryType = null)
        {
            this.Operation = operation;
            this.Children = children ?? Array.Empty<InteractionExpression>();
            this.Child = child;
            this.Subject = subject;
            this.Type = type;
            this.Property = property;
            this.Values = values ?? Array.Empty<string>();
            this.BoundaryType = boundaryType;
        }

        /// <summary>The operation represented by this node.</summary>
        internal enum OperationKind
        {
            /// <summary>Logical conjunction.</summary>
            All,

            /// <summary>Logical disjunction.</summary>
            Any,

            /// <summary>Logical negation.</summary>
            Not,

            /// <summary>Subject type equality.</summary>
            Type,

            /// <summary>Subject property value membership.</summary>
            Property,

            /// <summary>Specific crossed-boundary type.</summary>
            Crosses,
        }

        /// <summary>Gets the node operation.</summary>
        public OperationKind Operation { get; }

        /// <summary>Gets logical child expressions.</summary>
        public IReadOnlyList<InteractionExpression> Children { get; }

        /// <summary>Gets the negated child expression.</summary>
        public InteractionExpression? Child { get; }

        /// <summary>Gets the interaction subject.</summary>
        public string? Subject { get; }

        /// <summary>Gets the expected element type.</summary>
        public string? Type { get; }

        /// <summary>Gets the runtime property name.</summary>
        public string? Property { get; }

        /// <summary>Gets accepted property values.</summary>
        public IReadOnlyList<string> Values { get; }

        /// <summary>Gets the expected crossed-boundary type.</summary>
        public string? BoundaryType { get; }

        /// <summary>Creates a conjunction.</summary>
        /// <param name="children">The expressions that must all match.</param>
        /// <returns>The conjunction expression.</returns>
        public static InteractionExpression All(IReadOnlyList<InteractionExpression> children) =>
            new InteractionExpression(OperationKind.All, children: children);

        /// <summary>Creates a disjunction.</summary>
        /// <param name="children">The expressions of which at least one must match.</param>
        /// <returns>The disjunction expression.</returns>
        public static InteractionExpression Any(IReadOnlyList<InteractionExpression> children) =>
            new InteractionExpression(OperationKind.Any, children: children);

        /// <summary>Creates a negation.</summary>
        /// <param name="child">The expression to negate.</param>
        /// <returns>The negation expression.</returns>
        public static InteractionExpression Negate(InteractionExpression child) =>
            new InteractionExpression(OperationKind.Not, child: child);

        /// <summary>Creates a subject type predicate.</summary>
        /// <param name="subject">The interaction subject.</param>
        /// <param name="type">The expected type.</param>
        /// <returns>The type predicate.</returns>
        public static InteractionExpression TypeIs(string subject, string type) =>
            new InteractionExpression(OperationKind.Type, subject: subject, type: type);

        /// <summary>Creates a subject property predicate.</summary>
        /// <param name="subject">The interaction subject.</param>
        /// <param name="property">The runtime property name.</param>
        /// <param name="values">The accepted values.</param>
        /// <returns>The property predicate.</returns>
        public static InteractionExpression PropertyIn(string subject, string property, IReadOnlyList<string> values) =>
            new InteractionExpression(OperationKind.Property, subject: subject, property: property, values: values);

        /// <summary>Creates a crossed-boundary predicate.</summary>
        /// <param name="boundaryType">The expected boundary type.</param>
        /// <returns>The boundary predicate.</returns>
        public static InteractionExpression Crosses(string boundaryType) =>
            new InteractionExpression(OperationKind.Crosses, boundaryType: boundaryType);
    }
}
