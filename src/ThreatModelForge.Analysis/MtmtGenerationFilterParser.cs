namespace ThreatModelForge.Analysis
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Parses the MTMT GenerationFilters expression language into interaction-v1 expressions.
    /// </summary>
    internal sealed class MtmtGenerationFilterParser
    {
        private const int MaxExpressionLength = 65536;
        private const int MaxExpressionDepth = 64;
        private const int MaxExpressionNodes = 10000;

        private readonly MtmtGenerationFilterCatalog? catalog;
        private Lexer lexer = null!;
        private Token current;
        private int nodes;

        /// <summary>Initializes a new instance of the <see cref="MtmtGenerationFilterParser"/> class.</summary>
        /// <param name="catalog">The optional source catalog used to resolve references.</param>
        internal MtmtGenerationFilterParser(MtmtGenerationFilterCatalog? catalog = null)
        {
            this.catalog = catalog;
        }

        private enum TokenKind
        {
            End,
            Identifier,
            QuotedValue,
            Dot,
            LeftParenthesis,
            RightParenthesis,
        }

        /// <summary>Parses one non-empty source expression.</summary>
        /// <param name="expression">The MTMT GenerationFilters expression.</param>
        /// <returns>The compiled interaction expression.</returns>
        internal InteractionExpression Parse(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                throw new FormatException("GenerationFilters expression cannot be empty.");
            }

            if (expression.Length > MaxExpressionLength)
            {
                throw new InvalidDataException($"GenerationFilters expression exceeds the limit of {MaxExpressionLength} characters.");
            }

            this.lexer = new Lexer(expression);
            this.current = this.lexer.Next();
            this.nodes = 0;
            InteractionExpression result = this.ParseOr(depth: 1);
            if (this.current.Kind != TokenKind.End)
            {
                throw this.Error($"unexpected token '{this.current.Text}'");
            }

            ValidateExpression(result);
            return result;
        }

        /// <summary>Compiles a threat's Include and Exclude filters into one firing expression.</summary>
        /// <param name="include">The required Include expression.</param>
        /// <param name="exclude">The optional Exclude expression.</param>
        /// <returns><paramref name="include"/> AND NOT <paramref name="exclude"/> when Exclude is present.</returns>
        internal InteractionExpression Compile(string include, string? exclude)
        {
            InteractionExpression included = this.Parse(include);
            if (string.IsNullOrWhiteSpace(exclude))
            {
                return included;
            }

            InteractionExpression result = InteractionExpression.All(new[]
            {
                included,
                InteractionExpression.Negate(this.Parse(exclude!)),
            });
            ValidateExpression(result);
            return result;
        }

        private static void ValidateExpression(InteractionExpression expression)
        {
            int nodeCount = 0;
            ValidateExpression(expression, depth: 1, ref nodeCount);
        }

        private static void ValidateExpression(InteractionExpression expression, int depth, ref int nodeCount)
        {
            if (depth > MaxExpressionDepth)
            {
                throw new InvalidDataException(
                    $"GenerationFilters expression exceeds the depth limit of {MaxExpressionDepth}.");
            }

            nodeCount++;
            if (nodeCount > MaxExpressionNodes)
            {
                throw new InvalidDataException(
                    $"GenerationFilters expression exceeds the node limit of {MaxExpressionNodes}.");
            }

            if (expression.Operation == InteractionExpression.OperationKind.Type &&
                string.Equals(expression.Type, "ROOT", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(expression.Subject, "source", StringComparison.Ordinal))
            {
                throw new FormatException("GenerationFilters ROOT is only valid for source type predicates.");
            }

            if (expression.Child != null)
            {
                ValidateExpression(expression.Child, depth + 1, ref nodeCount);
            }

            foreach (InteractionExpression child in expression.Children)
            {
                ValidateExpression(child, depth + 1, ref nodeCount);
            }
        }

        private FormatException Error(string message)
        {
            return new FormatException($"Invalid GenerationFilters expression at offset {this.current.Position}: {message}.");
        }

        private InteractionExpression Create(InteractionExpression expression)
        {
            this.nodes++;
            if (this.nodes > MaxExpressionNodes)
            {
                throw new InvalidDataException($"GenerationFilters expression exceeds the node limit of {MaxExpressionNodes}.");
            }

            return expression;
        }

        private InteractionExpression ParseOr(int depth)
        {
            List<InteractionExpression> children = new List<InteractionExpression> { this.ParseAnd(depth) };
            while (this.MatchKeyword("or"))
            {
                children.Add(this.ParseAnd(depth));
            }

            return children.Count == 1 ? children[0] : this.Create(InteractionExpression.Any(children.AsReadOnly()));
        }

        private InteractionExpression ParseAnd(int depth)
        {
            List<InteractionExpression> children = new List<InteractionExpression> { this.ParseUnary(depth) };
            while (this.MatchKeyword("and"))
            {
                children.Add(this.ParseUnary(depth));
            }

            return children.Count == 1 ? children[0] : this.Create(InteractionExpression.All(children.AsReadOnly()));
        }

        private InteractionExpression ParseUnary(int depth)
        {
            this.ValidateDepth(depth);
            if (this.MatchKeyword("not"))
            {
                return this.Create(InteractionExpression.Negate(this.ParseUnary(depth + 1)));
            }

            if (this.current.Kind == TokenKind.LeftParenthesis)
            {
                this.Advance();
                InteractionExpression expression = this.ParseOr(depth + 1);
                this.Expect(TokenKind.RightParenthesis, "expected ')'");
                return expression;
            }

            return this.ParsePredicate();
        }

        private InteractionExpression ParsePredicate()
        {
            string subject = this.ExpectIdentifier("expected source, target, or flow").ToLowerInvariant();
            if (!string.Equals(subject, "source", StringComparison.Ordinal) &&
                !string.Equals(subject, "target", StringComparison.Ordinal) &&
                !string.Equals(subject, "flow", StringComparison.Ordinal))
            {
                throw this.Error($"unknown subject '{subject}'");
            }

            if (this.current.Kind == TokenKind.Dot)
            {
                this.Advance();
                string property = this.ExpectIdentifier("expected property after '.'");
                this.ExpectKeyword("is");
                string value = this.ExpectQuotedValue();
                property = this.catalog?.ResolvePropertyValue(property, value) ?? property;
                return this.Create(InteractionExpression.PropertyIn(subject, property, new[] { value }));
            }

            if (string.Equals(subject, "flow", StringComparison.Ordinal) && this.MatchKeyword("crosses"))
            {
                string boundaryType = this.ExpectQuotedValue();
                boundaryType = this.catalog?.ResolveType(boundaryType) ?? boundaryType;
                return this.Create(InteractionExpression.Crosses(boundaryType));
            }

            this.ExpectKeyword("is");
            string type = this.ExpectQuotedValue();
            type = this.catalog?.ResolveType(type) ?? type;
            return this.Create(InteractionExpression.TypeIs(subject, type));
        }

        private void ValidateDepth(int depth)
        {
            if (depth > MaxExpressionDepth)
            {
                throw new InvalidDataException($"GenerationFilters expression exceeds the depth limit of {MaxExpressionDepth}.");
            }
        }

        private string ExpectIdentifier(string message)
        {
            if (this.current.Kind != TokenKind.Identifier)
            {
                throw this.Error(message);
            }

            string value = this.current.Text;
            this.Advance();
            return value;
        }

        private string ExpectQuotedValue()
        {
            if (this.current.Kind != TokenKind.QuotedValue)
            {
                throw this.Error("expected a single-quoted value");
            }

            string value = this.current.Text;
            this.Advance();
            return value;
        }

        private void ExpectKeyword(string keyword)
        {
            if (!this.MatchKeyword(keyword))
            {
                throw this.Error($"expected '{keyword}'");
            }
        }

        private void Expect(TokenKind kind, string message)
        {
            if (this.current.Kind != kind)
            {
                throw this.Error(message);
            }

            this.Advance();
        }

        private bool MatchKeyword(string keyword)
        {
            if (this.current.Kind != TokenKind.Identifier ||
                !string.Equals(this.current.Text, keyword, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            this.Advance();
            return true;
        }

        private void Advance()
        {
            this.current = this.lexer.Next();
        }

        private readonly struct Token
        {
            public Token(TokenKind kind, string text, int position)
            {
                this.Kind = kind;
                this.Text = text;
                this.Position = position;
            }

            public TokenKind Kind { get; }

            public string Text { get; }

            public int Position { get; }
        }

        private sealed class Lexer
        {
            private readonly string expression;
            private int offset;

            public Lexer(string expression)
            {
                this.expression = expression;
            }

            public Token Next()
            {
                while (this.offset < this.expression.Length && char.IsWhiteSpace(this.expression[this.offset]))
                {
                    this.offset++;
                }

                if (this.offset == this.expression.Length)
                {
                    return new Token(TokenKind.End, string.Empty, this.offset);
                }

                int start = this.offset;
                char value = this.expression[this.offset++];
                if (value == '(')
                {
                    return new Token(TokenKind.LeftParenthesis, "(", start);
                }

                if (value == ')')
                {
                    return new Token(TokenKind.RightParenthesis, ")", start);
                }

                if (value == '.')
                {
                    return new Token(TokenKind.Dot, ".", start);
                }

                if (value == '\'')
                {
                    return this.ReadQuotedValue(start);
                }

                while (this.offset < this.expression.Length && !IsDelimiter(this.expression[this.offset]))
                {
                    this.offset++;
                }

                return new Token(TokenKind.Identifier, this.expression.Substring(start, this.offset - start), start);
            }

            private static bool IsDelimiter(char value)
            {
                return char.IsWhiteSpace(value) || value == '(' || value == ')' || value == '.' || value == '\'';
            }

            private Token ReadQuotedValue(int start)
            {
                StringBuilder value = new StringBuilder();
                while (this.offset < this.expression.Length)
                {
                    char current = this.expression[this.offset++];
                    if (current == '\'' &&
                        this.offset < this.expression.Length &&
                        this.expression[this.offset] == '\'')
                    {
                        value.Append('\'');
                        this.offset++;
                        continue;
                    }

                    if (current == '\'')
                    {
                        return new Token(TokenKind.QuotedValue, value.ToString(), start);
                    }

                    if (current == '\\' && this.offset < this.expression.Length &&
                        (this.expression[this.offset] == '\\' || this.expression[this.offset] == '\''))
                    {
                        value.Append(this.expression[this.offset++]);
                        continue;
                    }

                    value.Append(current);
                }

                throw new FormatException($"Invalid GenerationFilters expression at offset {start}: unterminated quoted value.");
            }
        }
    }
}
