using System;
using System.Collections.Generic;
using System.Globalization;

namespace Data;

public sealed class ConditionClause
{
  private readonly Func<Func<string, string>, bool> evaluator;
  public readonly string Source;

  internal ConditionClause(string source, Func<Func<string, string>, bool> evaluator)
  {
    Source = source;
    this.evaluator = evaluator;
  }

  public bool Evaluate(Functions f)
  {
    return Evaluate(r => f.Replace(r, false, true));
  }

  public bool Evaluate(Func<string, string> resolveValue)
  {
    try
    {
      return evaluator(resolveValue);
    }
    catch
    {
      return false;
    }
  }
}

public static class Conditions
{
  private const double NumericTolerance = 0.0000001;

  public static ConditionClause False(string source = "")
  {
    return new ConditionClause(source, _ => false);
  }

  public static bool TryParse(string condition, out ConditionClause? clause, out string error)
  {
    clause = null;
    error = "";
    if (string.IsNullOrWhiteSpace(condition))
    {
      error = "Condition is empty.";
      return false;
    }

    if (!Tokenizer.TryTokenize(condition, out var tokens, out error))
      return false;

    try
    {
      var parser = new Parser(tokens);
      var root = parser.ParseCondition();
      clause = new ConditionClause(condition, resolver => root.Evaluate(resolver));
      return true;
    }
    catch (Exception ex)
    {
      error = ex.Message;
      return false;
    }
  }

  private enum TokenType
  {
    Value,
    And,
    Or,
    Not,
    Xor,
    In,
    NotIn,
    Equal,
    NotEqual,
    Greater,
    Less,
    GreaterOrEqual,
    LessOrEqual,
    LeftParen,
    RightParen,
    End,
  }

  private readonly struct Token(TokenType type, string text, int position)
  {
    public readonly TokenType Type = type;
    public readonly string Text = text;
    public readonly int Position = position;
  }

  private static class Tokenizer
  {
    public static bool TryTokenize(string condition, out List<Token> tokens, out string error)
    {
      tokens = [];
      error = "";
      var index = 0;
      while (index < condition.Length)
      {
        var c = condition[index];
        if (char.IsWhiteSpace(c))
        {
          ++index;
          continue;
        }

        if (c == '(')
        {
          tokens.Add(new Token(TokenType.LeftParen, "(", index));
          ++index;
          continue;
        }

        if (c == ')')
        {
          tokens.Add(new Token(TokenType.RightParen, ")", index));
          ++index;
          continue;
        }

        if (c == '<')
        {
          if (TryReadFunctionValue(condition, index, out var functionValue, out var nextIndex))
          {
            tokens.Add(new Token(TokenType.Value, functionValue, index));
            index = nextIndex;
            continue;
          }
          if (TryRead(condition, "<=", index))
          {
            tokens.Add(new Token(TokenType.LessOrEqual, "<=", index));
            index += 2;
            continue;
          }
          tokens.Add(new Token(TokenType.Less, "<", index));
          ++index;
          continue;
        }

        if (c == '>')
        {
          if (TryRead(condition, ">=", index))
          {
            tokens.Add(new Token(TokenType.GreaterOrEqual, ">=", index));
            index += 2;
            continue;
          }
          tokens.Add(new Token(TokenType.Greater, ">", index));
          ++index;
          continue;
        }

        if (c == '=')
        {
          tokens.Add(new Token(TokenType.Equal, "=", index));
          ++index;
          continue;
        }

        if (c == '!')
        {
          if (TryRead(condition, "!=", index))
          {
            tokens.Add(new Token(TokenType.NotEqual, "!=", index));
            index += 2;
            continue;
          }
          tokens.Add(new Token(TokenType.Not, "!", index));
          ++index;
          continue;
        }

        if (c == '\'' || c == '"')
        {
          var nextIndex = index;
          if (!TryReadQuotedValue(condition, index, out var quotedValue, out nextIndex))
          {
            error = $"Unterminated quoted string at position {index + 1}.";
            return false;
          }
          tokens.Add(new Token(TokenType.Value, quotedValue, index));
          index = nextIndex;
          continue;
        }

        var valueStart = index;
        while (index < condition.Length && !char.IsWhiteSpace(condition[index]) && !IsDelimiter(condition, index))
          ++index;

        var value = condition.Substring(valueStart, index - valueStart);
        if (value.Length == 0)
        {
          error = $"Unexpected token at position {valueStart + 1}.";
          return false;
        }

        var keywordType = ToKeyword(value);
        tokens.Add(new Token(keywordType ?? TokenType.Value, value, valueStart));
      }

      tokens.Add(new Token(TokenType.End, "", condition.Length));
      return true;
    }

    private static bool TryReadFunctionValue(string condition, int index, out string functionValue, out int nextIndex)
    {
      functionValue = "";
      nextIndex = index;
      if (index + 1 >= condition.Length) return false;
      var firstInner = condition[index + 1];
      if (char.IsWhiteSpace(firstInner) || firstInner == '>' || firstInner == '=') return false;

      var depth = 0;
      for (var i = index; i < condition.Length; ++i)
      {
        var c = condition[i];
        if (c == '<')
          ++depth;
        else if (c == '>')
        {
          --depth;
          if (depth == 0)
          {
            functionValue = condition.Substring(index, i - index + 1);
            nextIndex = i + 1;
            return true;
          }
        }
      }

      return false;
    }

    private static bool TryReadQuotedValue(string condition, int index, out string value, out int nextIndex)
    {
      value = "";
      nextIndex = index;
      var quote = condition[index];
      var chars = new List<char>();
      var escaped = false;
      for (var i = index + 1; i < condition.Length; ++i)
      {
        var c = condition[i];
        if (escaped)
        {
          chars.Add(c);
          escaped = false;
          continue;
        }

        if (c == '\\')
        {
          escaped = true;
          continue;
        }

        if (c == quote)
        {
          value = new string([.. chars]);
          nextIndex = i + 1;
          return true;
        }

        chars.Add(c);
      }

      return false;
    }

    private static bool IsDelimiter(string condition, int index)
    {
      var c = condition[index];
      return c == '(' || c == ')' || c == '<' || c == '>' || c == '=' || c == '!';
    }

    private static TokenType? ToKeyword(string value)
    {
      var upper = value.ToUpperInvariant();
      return upper switch
      {
        "AND" => TokenType.And,
        "OR" => TokenType.Or,
        "NOT" => TokenType.Not,
        "XOR" => TokenType.Xor,
        "IN" => TokenType.In,
        _ => null,
      };
    }

    private static bool TryRead(string condition, string match, int index)
    {
      if (index + match.Length > condition.Length) return false;
      return string.CompareOrdinal(condition, index, match, 0, match.Length) == 0;
    }
  }

  private sealed class Parser(List<Token> tokens)
  {
    private readonly List<Token> tokens = tokens;
    private int index = 0;

    private Token Current => tokens[index];
    private Token Next => index + 1 < tokens.Count ? tokens[index + 1] : tokens[tokens.Count - 1];

    public ConditionNode ParseCondition()
    {
      var node = ParseOr();
      if (Current.Type != TokenType.End)
        throw new InvalidOperationException($"Unexpected token '{Current.Text}' at position {Current.Position + 1}.");
      return node;
    }

    private ConditionNode ParseOr()
    {
      var node = ParseXor();
      while (Current.Type == TokenType.Or)
      {
        ++index;
        node = new LogicalNode(LogicalType.Or, node, ParseXor());
      }
      return node;
    }

    private ConditionNode ParseXor()
    {
      var node = ParseAnd();
      while (Current.Type == TokenType.Xor)
      {
        ++index;
        node = new LogicalNode(LogicalType.Xor, node, ParseAnd());
      }
      return node;
    }

    private ConditionNode ParseAnd()
    {
      var node = ParseUnary();
      while (Current.Type == TokenType.And)
      {
        ++index;
        node = new LogicalNode(LogicalType.And, node, ParseUnary());
      }
      return node;
    }

    private ConditionNode ParseUnary()
    {
      if (Current.Type != TokenType.Not)
        return ParsePrimary();
      ++index;
      return new NotNode(ParseUnary());
    }

    private ConditionNode ParsePrimary()
    {
      if (Current.Type == TokenType.LeftParen)
      {
        ++index;
        var node = ParseOr();
        if (Current.Type != TokenType.RightParen)
          throw new InvalidOperationException($"Missing closing parenthesis at position {Current.Position + 1}.");
        ++index;
        return node;
      }

      return ParseComparisonOrValue();
    }

    private ConditionNode ParseComparisonOrValue()
    {
      var left = ParseValue();
      if (!IsComparisonToken(Current.Type) && !(Current.Type == TokenType.Not && Next.Type == TokenType.In))
        return new ValueNode(left);

      TokenType token;
      if (Current.Type == TokenType.Not)
      {
        token = TokenType.NotIn;
        index += 2;
      }
      else
      {
        token = Current.Type;
        ++index;
      }
      var right = ParseValue();
      return new ComparisonNode(token, left, right);
    }

    private string ParseValue()
    {
      if (Current.Type != TokenType.Value)
        throw new InvalidOperationException($"Expected value at position {Current.Position + 1}.");

      var value = Current.Text;
      ++index;
      return value;
    }

    private static bool IsComparisonToken(TokenType token)
    {
      if (token == TokenType.In) return true;
      return token == TokenType.Equal
        || token == TokenType.NotEqual
        || token == TokenType.Greater
        || token == TokenType.Less
        || token == TokenType.GreaterOrEqual
        || token == TokenType.LessOrEqual;
    }
  }

  private enum LogicalType
  {
    And,
    Or,
    Xor,
  }

  private abstract class ConditionNode
  {
    public abstract bool Evaluate(Func<string, string> resolve);
  }

  private sealed class ValueNode(string token) : ConditionNode
  {
    private readonly string token = token;
    public override bool Evaluate(Func<string, string> resolve)
    {
      var value = ResolveToken(token, resolve);
      return ToTruthy(value);
    }
  }

  private sealed class NotNode(ConditionNode node) : ConditionNode
  {
    private readonly ConditionNode node = node;
    public override bool Evaluate(Func<string, string> resolve)
    {
      return !node.Evaluate(resolve);
    }
  }

  private sealed class LogicalNode(LogicalType type, ConditionNode left, ConditionNode right) : ConditionNode
  {
    private readonly LogicalType type = type;
    private readonly ConditionNode left = left;
    private readonly ConditionNode right = right;

    public override bool Evaluate(Func<string, string> resolve)
    {
      var leftValue = left.Evaluate(resolve);
      if (type == LogicalType.And) return leftValue && right.Evaluate(resolve);
      if (type == LogicalType.Or) return leftValue || right.Evaluate(resolve);
      return leftValue ^ right.Evaluate(resolve);
    }
  }

  private sealed class ComparisonNode(TokenType operatorType, string leftToken, string rightToken) : ConditionNode
  {
    private readonly TokenType operatorType = operatorType;
    private readonly string leftToken = leftToken;
    private readonly string rightToken = rightToken;

    public override bool Evaluate(Func<string, string> resolve)
    {
      var left = ResolveToken(leftToken, resolve);
      var right = ResolveToken(rightToken, resolve);
      return Compare(operatorType, left, right);
    }
  }

  private static string ResolveToken(string token, Func<string, string> resolve)
  {
    return resolve(token) ?? "";
  }

  private static bool ToTruthy(string value)
  {
    var trimmed = value.Trim();
    if (trimmed == "") return false;
    if (string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase)) return false;
    if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
      return true;

    return Math.Abs(number) > NumericTolerance;
  }

  private static bool Compare(TokenType operatorType, string left, string right)
  {
    var leftTrimmed = left.Trim();
    var rightTrimmed = right.Trim();
    if (operatorType == TokenType.In || operatorType == TokenType.NotIn)
    {
      var contains = ContainsValue(rightTrimmed, leftTrimmed);
      return operatorType == TokenType.In ? contains : !contains;
    }
    var leftNumber = double.TryParse(leftTrimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var leftValue);
    var rightNumber = double.TryParse(rightTrimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var rightValue);

    if (operatorType == TokenType.Equal || operatorType == TokenType.NotEqual)
    {
      var equal = leftNumber && rightNumber
        ? Math.Abs(leftValue - rightValue) <= NumericTolerance
        : string.Equals(leftTrimmed, rightTrimmed, StringComparison.OrdinalIgnoreCase);
      return operatorType == TokenType.Equal ? equal : !equal;
    }

    if (!leftNumber || !rightNumber) return false;

    return operatorType switch
    {
      TokenType.Greater => leftValue > rightValue,
      TokenType.Less => leftValue < rightValue,
      TokenType.GreaterOrEqual => leftValue > rightValue || Math.Abs(leftValue - rightValue) <= NumericTolerance,
      TokenType.LessOrEqual => leftValue < rightValue || Math.Abs(leftValue - rightValue) <= NumericTolerance,
      _ => false,
    };
  }

  private static bool ContainsValue(string commaSeparatedValues, string value)
  {
    if (value == "") return false;
    return commaSeparatedValues.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
  }
}
