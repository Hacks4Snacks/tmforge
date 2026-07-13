namespace ThreatModelForge.Analysis
{
    /// <summary>
    /// Preserves one source-language expression that contributed to a compiled rule.
    /// </summary>
    public sealed class RuleSourceExpression
    {
        /// <summary>Initializes a new instance of the <see cref="RuleSourceExpression"/> class.</summary>
        /// <param name="role">The expression's source-defined role.</param>
        /// <param name="language">The namespaced source expression language.</param>
        /// <param name="text">The original expression text.</param>
        internal RuleSourceExpression(string role, string language, string text)
        {
            this.Role = role;
            this.Language = language;
            this.Text = text;
        }

        /// <summary>Gets the expression's source-defined role, such as <c>include</c>.</summary>
        public string Role { get; }

        /// <summary>Gets the namespaced source expression language.</summary>
        public string Language { get; }

        /// <summary>Gets the original expression text.</summary>
        public string Text { get; }
    }
}
