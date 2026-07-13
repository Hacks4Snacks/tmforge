namespace ThreatModelForge.Analysis
{
    using System;
    using System.Linq;

    /// <summary>
    /// Produces a canonical, precedence-preserving MTMT expression for parser regression tests.
    /// </summary>
    internal static class MtmtGenerationFilterFormatter
    {
        /// <summary>Formats one parsed interaction expression.</summary>
        /// <param name="expression">The parsed expression.</param>
        /// <returns>A canonical MTMT expression.</returns>
        internal static string Format(InteractionExpression expression)
        {
            _ = expression ?? throw new ArgumentNullException(nameof(expression));
            switch (expression.Operation)
            {
                case InteractionExpression.OperationKind.All:
                    return "(" + string.Join(" and ", expression.Children.Select(Format)) + ")";
                case InteractionExpression.OperationKind.Any:
                    return "(" + string.Join(" or ", expression.Children.Select(Format)) + ")";
                case InteractionExpression.OperationKind.Not:
                    return "not " + Format(expression.Child!);
                case InteractionExpression.OperationKind.Crosses:
                    return "flow crosses '" + Escape(expression.BoundaryType!) + "'";
                case InteractionExpression.OperationKind.Type:
                    return expression.Subject + " is '" + Escape(expression.Type!) + "'";
                case InteractionExpression.OperationKind.Property:
                    if (expression.Values.Count != 1)
                    {
                        throw new InvalidOperationException("MTMT property predicates require exactly one value.");
                    }

                    return expression.Subject + "." + expression.Property + " is '" + Escape(expression.Values[0]) + "'";
                default:
                    throw new InvalidOperationException("Unknown interaction expression operation.");
            }
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("'", "''");
        }
    }
}
