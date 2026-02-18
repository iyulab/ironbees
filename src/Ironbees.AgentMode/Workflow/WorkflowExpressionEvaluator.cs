using System.Globalization;

namespace Ironbees.AgentMode.Workflow;

/// <summary>
/// Lightweight recursive descent expression evaluator for YAML workflow conditions.
/// Supports boolean operators (AND, OR, NOT), comparison operators, variable references
/// (status, iteration_count, output.*), and literal values.
/// </summary>
internal static class WorkflowExpressionEvaluator
{
    /// <summary>
    /// Evaluates a condition expression against the given workflow runtime state.
    /// Returns <c>true</c> for null/empty conditions (unconditional transitions).
    /// </summary>
    public static bool Evaluate(string? expression, WorkflowRuntimeState state)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return true;
        }

        var tokens = Tokenize(expression);
        var context = new ParseContext(tokens, state);
        var result = ParseOrExpression(context);

        return IsTrue(result);
    }

    #region Tokenizer

    private static List<Token> Tokenize(string input)
    {
        var tokens = new List<Token>();
        var i = 0;

        while (i < input.Length)
        {
            // Skip whitespace
            if (char.IsWhiteSpace(input[i]))
            {
                i++;
                continue;
            }

            // Two-character operators
            if (i + 1 < input.Length)
            {
                var twoChar = input.Substring(i, 2);
                switch (twoChar)
                {
                    case "&&":
                        tokens.Add(new Token(TokenType.And, twoChar));
                        i += 2;
                        continue;
                    case "||":
                        tokens.Add(new Token(TokenType.Or, twoChar));
                        i += 2;
                        continue;
                    case ">=":
                        tokens.Add(new Token(TokenType.GreaterEqual, twoChar));
                        i += 2;
                        continue;
                    case "<=":
                        tokens.Add(new Token(TokenType.LessEqual, twoChar));
                        i += 2;
                        continue;
                    case "==":
                        tokens.Add(new Token(TokenType.Equal, twoChar));
                        i += 2;
                        continue;
                    case "!=":
                        tokens.Add(new Token(TokenType.NotEqual, twoChar));
                        i += 2;
                        continue;
                }
            }

            // Single-character operators
            switch (input[i])
            {
                case '!':
                    tokens.Add(new Token(TokenType.Not, "!"));
                    i++;
                    continue;
                case '>':
                    tokens.Add(new Token(TokenType.Greater, ">"));
                    i++;
                    continue;
                case '<':
                    tokens.Add(new Token(TokenType.Less, "<"));
                    i++;
                    continue;
                case '(':
                    tokens.Add(new Token(TokenType.LeftParen, "("));
                    i++;
                    continue;
                case ')':
                    tokens.Add(new Token(TokenType.RightParen, ")"));
                    i++;
                    continue;
            }

            // Quoted string literal
            if (input[i] is '"' or '\'')
            {
                var quote = input[i];
                var start = i + 1;
                i++;
                while (i < input.Length && input[i] != quote)
                {
                    i++;
                }

                var value = input[start..i];
                if (i < input.Length)
                {
                    i++; // skip closing quote
                }

                tokens.Add(new Token(TokenType.String, value));
                continue;
            }

            // Numbers and identifiers
            if (char.IsLetterOrDigit(input[i]) || input[i] == '_' || input[i] == '.')
            {
                var start = i;
                while (i < input.Length && (char.IsLetterOrDigit(input[i]) || input[i] == '_' || input[i] == '.'))
                {
                    i++;
                }

                var word = input[start..i];
                tokens.Add(ClassifyWord(word));
                continue;
            }

            // Unknown character - skip
            i++;
        }

        tokens.Add(new Token(TokenType.End, string.Empty));
        return tokens;
    }

    private static Token ClassifyWord(string word)
    {
        // Boolean literals
        if (word.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return new Token(TokenType.Boolean, "true");
        }

        if (word.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return new Token(TokenType.Boolean, "false");
        }

        // Numeric literal
        if (double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            return new Token(TokenType.Number, word);
        }

        // Identifier (variable reference)
        return new Token(TokenType.Identifier, word);
    }

    #endregion

    #region Parser

    /// <summary>
    /// or_expr → and_expr ('||' and_expr)*
    /// </summary>
    private static object? ParseOrExpression(ParseContext ctx)
    {
        var left = ParseAndExpression(ctx);

        while (ctx.Current.Type == TokenType.Or)
        {
            ctx.Advance();
            var right = ParseAndExpression(ctx);
            left = IsTrue(left) || IsTrue(right);
        }

        return left;
    }

    /// <summary>
    /// and_expr → unary_expr ('&amp;&amp;' unary_expr)*
    /// </summary>
    private static object? ParseAndExpression(ParseContext ctx)
    {
        var left = ParseUnaryExpression(ctx);

        while (ctx.Current.Type == TokenType.And)
        {
            ctx.Advance();
            var right = ParseUnaryExpression(ctx);
            left = IsTrue(left) && IsTrue(right);
        }

        return left;
    }

    /// <summary>
    /// unary_expr → '!' unary_expr | comparison
    /// </summary>
    private static object? ParseUnaryExpression(ParseContext ctx)
    {
        if (ctx.Current.Type == TokenType.Not)
        {
            ctx.Advance();
            var operand = ParseUnaryExpression(ctx);
            return !IsTrue(operand);
        }

        return ParseComparison(ctx);
    }

    /// <summary>
    /// comparison → primary (comp_op primary)?
    /// </summary>
    private static object? ParseComparison(ParseContext ctx)
    {
        var left = ParsePrimary(ctx);

        if (IsComparisonOperator(ctx.Current.Type))
        {
            var op = ctx.Current.Type;
            ctx.Advance();
            var right = ParsePrimary(ctx);
            return EvaluateComparison(left, op, right);
        }

        return left;
    }

    /// <summary>
    /// primary → '(' or_expr ')' | literal | variable
    /// </summary>
    private static object? ParsePrimary(ParseContext ctx)
    {
        if (ctx.Current.Type == TokenType.LeftParen)
        {
            ctx.Advance();
            var result = ParseOrExpression(ctx);
            if (ctx.Current.Type == TokenType.RightParen)
            {
                ctx.Advance();
            }

            return result;
        }

        if (ctx.Current.Type == TokenType.Number)
        {
            var value = double.Parse(ctx.Current.Value, CultureInfo.InvariantCulture);
            ctx.Advance();
            return value;
        }

        if (ctx.Current.Type == TokenType.String)
        {
            var value = ctx.Current.Value;
            ctx.Advance();
            return value;
        }

        if (ctx.Current.Type == TokenType.Boolean)
        {
            var value = ctx.Current.Value.Equals("true", StringComparison.OrdinalIgnoreCase);
            ctx.Advance();
            return value;
        }

        if (ctx.Current.Type == TokenType.Identifier)
        {
            var name = ctx.Current.Value;
            ctx.Advance();
            return ResolveVariable(name, ctx.State);
        }

        // End of input or unexpected token — return null (falsy)
        return null;
    }

    #endregion

    #region Variable Resolution

    private static object? ResolveVariable(string name, WorkflowRuntimeState state)
    {
        // Backwards-compatible status keywords
        var lowerName = name.ToLowerInvariant();
        switch (lowerName)
        {
            case "success":
                return state.Status == WorkflowExecutionStatus.Running;
            case "failure":
                return state.Status == WorkflowExecutionStatus.Failed;
        }

        // Direct property access
        switch (lowerName)
        {
            case "status":
                return state.Status.ToString().ToLowerInvariant();
            case "iteration_count":
                return (double)state.IterationCount;
        }

        // Output data access: "output.key" or "build.success" style (dot-separated)
        // Try "output." prefix first
        if (lowerName.StartsWith("output.", StringComparison.Ordinal) && lowerName.Length > 7)
        {
            var key = name[7..]; // preserve original casing for key
            if (state.OutputData.TryGetValue(key, out var outputValue))
            {
                return outputValue;
            }

            return null;
        }

        // Legacy dot-notation: "build.success" → OutputData["build_success"]
        if (name.Contains('.'))
        {
            var key = name.Replace('.', '_');
            if (state.OutputData.TryGetValue(key, out var outputValue))
            {
                return outputValue;
            }

            return null;
        }

        // Try direct OutputData lookup
        if (state.OutputData.TryGetValue(name, out var directValue))
        {
            return directValue;
        }

        return null;
    }

    #endregion

    #region Comparison Evaluation

    private static bool IsComparisonOperator(TokenType type) =>
        type is TokenType.Greater or TokenType.GreaterEqual
            or TokenType.Less or TokenType.LessEqual
            or TokenType.Equal or TokenType.NotEqual;

    private static bool EvaluateComparison(object? left, TokenType op, object? right)
    {
        // Numeric comparison
        if (TryGetNumber(left, out var leftNum) && TryGetNumber(right, out var rightNum))
        {
            return op switch
            {
                TokenType.Greater => leftNum > rightNum,
                TokenType.GreaterEqual => leftNum >= rightNum,
                TokenType.Less => leftNum < rightNum,
                TokenType.LessEqual => leftNum <= rightNum,
                TokenType.Equal => Math.Abs(leftNum - rightNum) < 0.0001,
                TokenType.NotEqual => Math.Abs(leftNum - rightNum) >= 0.0001,
                _ => false
            };
        }

        // Boolean equality
        if (left is bool leftBool && right is bool rightBool)
        {
            return op switch
            {
                TokenType.Equal => leftBool == rightBool,
                TokenType.NotEqual => leftBool != rightBool,
                _ => false
            };
        }

        // String comparison (equality only)
        var leftStr = left?.ToString();
        var rightStr = right?.ToString();

        return op switch
        {
            TokenType.Equal => string.Equals(leftStr, rightStr, StringComparison.OrdinalIgnoreCase),
            TokenType.NotEqual => !string.Equals(leftStr, rightStr, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool TryGetNumber(object? value, out double number)
    {
        switch (value)
        {
            case double d:
                number = d;
                return true;
            case int i:
                number = i;
                return true;
            case long l:
                number = l;
                return true;
            case float f:
                number = f;
                return true;
            default:
                if (value is string s &&
                    double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                {
                    return true;
                }

                number = 0;
                return false;
        }
    }

    #endregion

    #region Truthiness

    private static bool IsTrue(object? value)
    {
        return value switch
        {
            bool b => b,
            double d => d != 0,
            int i => i != 0,
            long l => l != 0,
            string s => !string.IsNullOrEmpty(s) &&
                        !s.Equals("false", StringComparison.OrdinalIgnoreCase) &&
                        !s.Equals("0", StringComparison.Ordinal),
            null => false,
            _ => true
        };
    }

    #endregion

    #region Types

    private enum TokenType
    {
        Identifier,
        Number,
        String,
        Boolean,
        And,
        Or,
        Not,
        Greater,
        GreaterEqual,
        Less,
        LessEqual,
        Equal,
        NotEqual,
        LeftParen,
        RightParen,
        End
    }

    private readonly record struct Token(TokenType Type, string Value);

    private sealed class ParseContext(List<Token> tokens, WorkflowRuntimeState state)
    {
        private int _position;

        public WorkflowRuntimeState State { get; } = state;

        public Token Current => _position < tokens.Count
            ? tokens[_position]
            : new Token(TokenType.End, string.Empty);

        public void Advance()
        {
            if (_position < tokens.Count)
            {
                _position++;
            }
        }
    }

    #endregion
}
