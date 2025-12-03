// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

namespace Agent.Core.Helpers.JmesPath;

/// <summary>
/// Top down operator precedence parser.
///
/// This is an implementation of Vaughan R. Pratt's
/// "Top Down Operator Precedence" parser.
/// (http://dl.acm.org/citation.cfm?doid=512927.512931).
///
/// These are some additional resources that help explain the
/// general idea behind a Pratt parser:
///
/// * http://effbot.org/zone/simple-top-down-parsing.htm
/// * http://javascript.crockford.com/tdop/tdop.html
///
/// A few notes on the implementation:
///
/// * All the nud/led tokens are dispatched using reflection/method dispatch.
///   This keeps all the parsing logic contained to a single class.
/// * We use two passes through the data. One to create a list of tokens,
///   then one pass through the tokens to create the AST. While the lexer could
///   yield tokens, we convert it to a list so we can easily implement two tokens
///   of lookahead. Creating a token list first is actually faster than
///   consuming from the token iterator one token at a time.
/// * The average JMESPath expression typically does not have a large amount
///   of tokens so this is not an issue.
/// </summary>
public class Parser
{
    private static readonly Dictionary<string, int> BindingPower = new()
    {
        { "eof", 0 },
        { "unquoted_identifier", 0 },
        { "quoted_identifier", 0 },
        { "literal", 0 },
        { "rbracket", 0 },
        { "rparen", 0 },
        { "comma", 0 },
        { "rbrace", 0 },
        { "number", 0 },
        { "current", 0 },
        { "expref", 0 },
        { "colon", 0 },
        { "pipe", 1 },
        { "or", 2 },
        { "and", 3 },
        { "eq", 5 },
        { "gt", 5 },
        { "lt", 5 },
        { "gte", 5 },
        { "lte", 5 },
        { "ne", 5 },
        { "flatten", 9 },
        { "star", 20 },
        { "filter", 21 },
        { "dot", 40 },
        { "not", 45 },
        { "lbrace", 50 },
        { "lbracket", 55 },
        { "lparen", 60 }
    };

    private const int ProjectionStop = 10;
    private const int MaxCacheSize = 128;
    private static readonly Dictionary<string, ParsedResult> Cache = new();
    private static readonly Random Random = new();

    private List<Token> _tokens = new();
    private int _index;

    public ParsedResult Parse(string expression)
    {
        if (Cache.TryGetValue(expression, out var cached))
        {
            return cached;
        }

        var parsedResult = DoParse(expression);
        Cache[expression] = parsedResult;

        if (Cache.Count > MaxCacheSize)
        {
            FreeCacheEntries();
        }

        return parsedResult;
    }

    private ParsedResult DoParse(string expression)
    {
        try
        {
            return ParseInternal(expression);
        }
        catch (LexerException e)
        {
            e.Expression = expression;
            throw;
        }
        catch (IncompleteExpressionException e)
        {
            e.SetExpression(expression);
            throw;
        }
        catch (ParseException e)
        {
            e.Expression = expression;
            throw;
        }
    }

    private ParsedResult ParseInternal(string expression)
    {
        var lexer = new Lexer();
        _tokens = lexer.Tokenize(expression);
        _index = 0;

        var parsed = Expression(0);

        if (CurrentToken() != "eof")
        {
            var t = LookaheadToken(0);
            throw new ParseException(t.Start, t.Value?.ToString(), t.Type,
                $"Unexpected token: {t.Value}");
        }

        return new ParsedResult(expression, parsed);
    }

    private AstNode Expression(int bindingPower = 0)
    {
        var leftToken = LookaheadToken(0);
        Advance();

        var left = TokenNud(leftToken);
        var currentToken = CurrentToken();

        while (bindingPower < BindingPower[currentToken])
        {
            var led = GetTokenLed(currentToken);
            if (led == null)
            {
                var errorToken = LookaheadToken(0);
                throw new ParseException(errorToken.Start, errorToken.Value?.ToString(),
                    errorToken.Type, "invalid token");
            }

            Advance();
            left = led(left);
            currentToken = CurrentToken();
        }

        return left;
    }

    // NUD (Null Denotation) methods - handle tokens at the start of an expression
    private AstNode TokenNud(Token token)
    {
        return token.Type switch
        {
            "literal" => Ast.Literal(token.Value),
            "unquoted_identifier" => Ast.Field(token.Value?.ToString() ?? ""),
            "quoted_identifier" => TokenNudQuotedIdentifier(token),
            "star" => TokenNudStar(),
            "filter" => TokenLedFilter(Ast.Identity()),
            "lbrace" => ParseMultiSelectHash(),
            "lparen" => TokenNudLparen(),
            "flatten" => TokenNudFlatten(),
            "not" => TokenNudNot(),
            "lbracket" => TokenNudLbracket(),
            "current" => Ast.CurrentNode(),
            "expref" => TokenNudExpref(),
            "eof" => throw new IncompleteExpressionException(token.Start,
                token.Value?.ToString(), token.Type),
            _ => throw new ParseException(token.Start, token.Value?.ToString(),
                token.Type, "invalid token")
        };
    }

    private AstNode TokenNudQuotedIdentifier(Token token)
    {
        var field = Ast.Field(token.Value?.ToString() ?? "");
        if (CurrentToken() == "lparen")
        {
            var t = LookaheadToken(0);
            throw new ParseException(0, t.Value?.ToString(), t.Type,
                "Quoted identifier not allowed for function names.");
        }
        return field;
    }

    private AstNode TokenNudStar()
    {
        var left = Ast.Identity();
        AstNode right;

        if (CurrentToken() == "rbracket")
        {
            right = Ast.Identity();
        }
        else
        {
            right = ParseProjectionRhs(BindingPower["star"]);
        }

        return Ast.ValueProjection(left, right);
    }

    private AstNode TokenNudLparen()
    {
        var expression = Expression();
        Match("rparen");
        return expression;
    }

    private AstNode TokenNudFlatten()
    {
        var left = Ast.Flatten(Ast.Identity());
        var right = ParseProjectionRhs(BindingPower["flatten"]);
        return Ast.Projection(left, right);
    }

    private AstNode TokenNudNot()
    {
        var expr = Expression(BindingPower["not"]);
        return Ast.NotExpression(expr);
    }

    private AstNode TokenNudLbracket()
    {
        if (CurrentToken() is "number" or "colon")
        {
            var right = ParseIndexExpression();
            return ProjectIfSlice(Ast.Identity(), right);
        }
        else if (CurrentToken() == "star" && Lookahead(1) == "rbracket")
        {
            Advance();
            Advance();
            var right = ParseProjectionRhs(BindingPower["star"]);
            return Ast.Projection(Ast.Identity(), right);
        }
        else
        {
            return ParseMultiSelectList();
        }
    }

    private AstNode TokenNudExpref()
    {
        var expression = Expression(BindingPower["expref"]);
        return Ast.Expref(expression);
    }

    // LED (Left Denotation) methods - handle infix/postfix operators
    private Func<AstNode, AstNode>? GetTokenLed(string tokenType)
    {
        return tokenType switch
        {
            "dot" => TokenLedDot,
            "pipe" => TokenLedPipe,
            "or" => TokenLedOr,
            "and" => TokenLedAnd,
            "lparen" => TokenLedLparen,
            "filter" => TokenLedFilter,
            "eq" => left => ParseComparator(left, "eq"),
            "ne" => left => ParseComparator(left, "ne"),
            "gt" => left => ParseComparator(left, "gt"),
            "gte" => left => ParseComparator(left, "gte"),
            "lt" => left => ParseComparator(left, "lt"),
            "lte" => left => ParseComparator(left, "lte"),
            "flatten" => TokenLedFlatten,
            "lbracket" => TokenLedLbracket,
            _ => null
        };
    }

    private AstNode TokenLedDot(AstNode left)
    {
        if (CurrentToken() != "star")
        {
            var right = ParseDotRhs(BindingPower["dot"]);
            if (left.Type == "subexpression")
            {
                left.Children.Add(right);
                return left;
            }
            else
            {
                return Ast.Subexpression(new List<AstNode> { left, right });
            }
        }
        else
        {
            Advance();
            var right = ParseProjectionRhs(BindingPower["dot"]);
            return Ast.ValueProjection(left, right);
        }
    }

    private AstNode TokenLedPipe(AstNode left)
    {
        var right = Expression(BindingPower["pipe"]);
        return Ast.Pipe(left, right);
    }

    private AstNode TokenLedOr(AstNode left)
    {
        var right = Expression(BindingPower["or"]);
        return Ast.OrExpression(left, right);
    }

    private AstNode TokenLedAnd(AstNode left)
    {
        var right = Expression(BindingPower["and"]);
        return Ast.AndExpression(left, right);
    }

    private AstNode TokenLedLparen(AstNode left)
    {
        if (left.Type != "field")
        {
            var prevT = LookaheadToken(-2);
            throw new ParseException(prevT.Start, prevT.Value?.ToString(), prevT.Type,
                $"Invalid function name '{prevT.Value}'");
        }

        var name = left.Value?.ToString() ?? "";
        var args = new List<AstNode>();

        while (CurrentToken() != "rparen")
        {
            var expression = Expression();
            if (CurrentToken() == "comma")
            {
                Match("comma");
            }
            args.Add(expression);
        }

        Match("rparen");
        return Ast.FunctionExpression(name, args);
    }

    private AstNode TokenLedFilter(AstNode left)
    {
        var condition = Expression(0);
        Match("rbracket");

        AstNode right;
        if (CurrentToken() == "flatten")
        {
            right = Ast.Identity();
        }
        else
        {
            right = ParseProjectionRhs(BindingPower["filter"]);
        }

        return Ast.FilterProjection(left, right, condition);
    }

    private AstNode TokenLedFlatten(AstNode left)
    {
        left = Ast.Flatten(left);
        var right = ParseProjectionRhs(BindingPower["flatten"]);
        return Ast.Projection(left, right);
    }

    private AstNode TokenLedLbracket(AstNode left)
    {
        var token = LookaheadToken(0);

        if (token.Type is "number" or "colon")
        {
            var right = ParseIndexExpression();
            if (left.Type == "index_expression")
            {
                left.Children.Add(right);
                return left;
            }
            else
            {
                return ProjectIfSlice(left, right);
            }
        }
        else
        {
            Match("star");
            Match("rbracket");
            var right = ParseProjectionRhs(BindingPower["star"]);
            return Ast.Projection(left, right);
        }
    }

    // Parsing helper methods
    private AstNode ParseIndexExpression()
    {
        if (Lookahead(0) == "colon" || Lookahead(1) == "colon")
        {
            return ParseSliceExpression();
        }
        else
        {
            var node = Ast.Index((int)(LookaheadToken(0).Value ?? 0));
            Advance();
            Match("rbracket");
            return node;
        }
    }

    private AstNode ParseSliceExpression()
    {
        var parts = new int?[] { null, null, null };
        var index = 0;
        var currentToken = CurrentToken();

        while (currentToken != "rbracket" && index < 3)
        {
            if (currentToken == "colon")
            {
                index++;
                if (index == 3)
                {
                    throw new ParseException(LookaheadToken(0).Start,
                        LookaheadToken(0).Value?.ToString(),
                        LookaheadToken(0).Type, "syntax error");
                }
                Advance();
            }
            else if (currentToken == "number")
            {
                parts[index] = (int)(LookaheadToken(0).Value ?? 0);
                Advance();
            }
            else
            {
                throw new ParseException(LookaheadToken(0).Start,
                    LookaheadToken(0).Value?.ToString(),
                    LookaheadToken(0).Type, "syntax error");
            }
            currentToken = CurrentToken();
        }

        Match("rbracket");
        return Ast.Slice(parts[0], parts[1], parts[2]);
    }

    private AstNode ParseComparator(AstNode left, string comparator)
    {
        var right = Expression(BindingPower[comparator]);
        return Ast.Comparator(comparator, left, right);
    }

    private AstNode ParseMultiSelectList()
    {
        var expressions = new List<AstNode>();

        while (true)
        {
            var expression = Expression();
            expressions.Add(expression);

            if (CurrentToken() == "rbracket")
            {
                break;
            }
            else
            {
                Match("comma");
            }
        }

        Match("rbracket");
        return Ast.MultiSelectList(expressions);
    }

    private AstNode ParseMultiSelectHash()
    {
        var pairs = new List<AstNode>();

        while (true)
        {
            var keyToken = LookaheadToken(0);
            MatchMultipleTokens(new[] { "quoted_identifier", "unquoted_identifier" });
            var keyName = keyToken.Value?.ToString() ?? "";
            Match("colon");
            var value = Expression(0);
            var node = Ast.KeyValPair(keyName, value);
            pairs.Add(node);

            if (CurrentToken() == "comma")
            {
                Match("comma");
            }
            else if (CurrentToken() == "rbrace")
            {
                Match("rbrace");
                break;
            }
        }

        return Ast.MultiSelectDict(pairs);
    }

    private AstNode ParseProjectionRhs(int bindingPower)
    {
        if (BindingPower[CurrentToken()] < ProjectionStop)
        {
            return Ast.Identity();
        }
        else if (CurrentToken() is "lbracket" or "filter")
        {
            return Expression(bindingPower);
        }
        else if (CurrentToken() == "dot")
        {
            Match("dot");
            return ParseDotRhs(bindingPower);
        }
        else
        {
            throw new ParseException(LookaheadToken(0).Start,
                LookaheadToken(0).Value?.ToString(),
                LookaheadToken(0).Type, "syntax error");
        }
    }

    private AstNode ParseDotRhs(int bindingPower)
    {
        var lookahead = CurrentToken();

        if (lookahead is "quoted_identifier" or "unquoted_identifier" or "star")
        {
            return Expression(bindingPower);
        }
        else if (lookahead == "lbracket")
        {
            Match("lbracket");
            return ParseMultiSelectList();
        }
        else if (lookahead == "lbrace")
        {
            Match("lbrace");
            return ParseMultiSelectHash();
        }
        else
        {
            var t = LookaheadToken(0);
            var allowed = new[] { "quoted_identifier", "unquoted_identifier", "lbracket", "lbrace" };
            var msg = $"Expecting: [{string.Join(", ", allowed)}], got: {t.Type}";
            throw new ParseException(t.Start, t.Value?.ToString(), t.Type, msg);
        }
    }

    private AstNode ProjectIfSlice(AstNode left, AstNode right)
    {
        var indexExpr = Ast.IndexExpression(new List<AstNode> { left, right });

        if (right.Type == "slice")
        {
            return Ast.Projection(indexExpr, ParseProjectionRhs(BindingPower["star"]));
        }
        else
        {
            return indexExpr;
        }
    }

    // Token navigation methods
    private void Advance()
    {
        _index++;
    }

    private string CurrentToken()
    {
        return _tokens[_index].Type;
    }

    private string Lookahead(int number)
    {
        return _tokens[_index + number].Type;
    }

    private Token LookaheadToken(int number)
    {
        return _tokens[_index + number];
    }

    private void Match(string tokenType)
    {
        if (CurrentToken() == tokenType)
        {
            Advance();
        }
        else
        {
            var token = LookaheadToken(0);
            if (token.Type == "eof")
            {
                throw new IncompleteExpressionException(token.Start,
                    token.Value?.ToString(), token.Type);
            }
            var message = $"Expecting: {tokenType}, got: {token.Type}";
            throw new ParseException(token.Start, token.Value?.ToString(),
                token.Type, message);
        }
    }

    private void MatchMultipleTokens(string[] tokenTypes)
    {
        if (!tokenTypes.Contains(CurrentToken()))
        {
            var token = LookaheadToken(0);
            if (token.Type == "eof")
            {
                throw new IncompleteExpressionException(token.Start,
                    token.Value?.ToString(), token.Type);
            }
            var message = $"Expecting: [{string.Join(", ", tokenTypes)}], got: {token.Type}";
            throw new ParseException(token.Start, token.Value?.ToString(),
                token.Type, message);
        }
        Advance();
    }

    private void FreeCacheEntries()
    {
        var keys = Cache.Keys.ToList();
        var toRemove = keys.OrderBy(_ => Random.Next()).Take(MaxCacheSize / 2);
        foreach (var key in toRemove)
        {
            Cache.Remove(key);
        }
    }

    public static void PurgeCache()
    {
        Cache.Clear();
    }
}

/// <summary>
/// Result of parsing a JMESPath expression.
/// </summary>
public class ParsedResult
{
    public string Expression { get; }
    public AstNode Parsed { get; }

    public ParsedResult(string expression, AstNode parsed)
    {
        Expression = expression;
        Parsed = parsed;
    }

    public override string ToString()
    {
        return Parsed.ToString() ?? "";
    }
}
