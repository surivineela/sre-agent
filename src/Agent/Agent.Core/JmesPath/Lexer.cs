// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Agent.Core.Helpers.JmesPath;

/// <summary>
/// Token produced by the lexer.
/// </summary>
public class Token
{
    public string Type { get; set; } = string.Empty;
    public object? Value { get; set; }
    public int Start { get; set; }
    public int End { get; set; }

    public Token() { }

    public Token(string type, object? value, int start, int end)
    {
        Type = type;
        Value = value;
        Start = start;
        End = end;
    }
}

/// <summary>
/// Lexer for tokenizing JMESPath expressions.
/// </summary>
public class Lexer
{
    private static readonly HashSet<char> StartIdentifier = new(
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_");

    private static readonly HashSet<char> ValidIdentifier = new(
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_");

    private static readonly HashSet<char> ValidNumber = new("0123456789");

    private static readonly HashSet<char> Whitespace = new(" \t\n\r");

    private static readonly Dictionary<char, string> SimpleTokens = new()
    {
        { '.', "dot" },
        { '*', "star" },
        { ']', "rbracket" },
        { ',', "comma" },
        { ':', "colon" },
        { '@', "current" },
        { '(', "lparen" },
        { ')', "rparen" },
        { '{', "lbrace" },
        { '}', "rbrace" }
    };

    private string _expression = string.Empty;
    private char[] _chars = Array.Empty<char>();
    private char? _current;
    private int _position;
    private int _length;

    public List<Token> Tokenize(string expression)
    {
        InitializeForExpression(expression);
        var tokens = new List<Token>();

        while (_current.HasValue)
        {
            if (SimpleTokens.TryGetValue(_current.Value, out var tokenType))
            {
                tokens.Add(new Token(tokenType, _current.Value.ToString(), _position, _position + 1));
                Next();
            }
            else if (StartIdentifier.Contains(_current.Value))
            {
                var start = _position;
                var buff = new StringBuilder();
                buff.Append(_current.Value);
                while (Next().HasValue && ValidIdentifier.Contains(_current.Value))
                {
                    buff.Append(_current.Value);
                }
                var value = buff.ToString();
                tokens.Add(new Token("unquoted_identifier", value, start, start + value.Length));
            }
            else if (Whitespace.Contains(_current.Value))
            {
                Next();
            }
            else if (_current.Value == '[')
            {
                var start = _position;
                var nextChar = Next();
                if (nextChar == ']')
                {
                    Next();
                    tokens.Add(new Token("flatten", "[]", start, start + 2));
                }
                else if (nextChar == '?')
                {
                    Next();
                    tokens.Add(new Token("filter", "[?", start, start + 2));
                }
                else
                {
                    tokens.Add(new Token("lbracket", "[", start, start + 1));
                }
            }
            else if (_current.Value == '\'')
            {
                tokens.Add(ConsumeRawStringLiteral());
            }
            else if (_current.Value == '|')
            {
                tokens.Add(MatchOrElse('|', "or", "pipe"));
            }
            else if (_current.Value == '&')
            {
                tokens.Add(MatchOrElse('&', "and", "expref"));
            }
            else if (_current.Value == '`')
            {
                tokens.Add(ConsumeLiteral());
            }
            else if (ValidNumber.Contains(_current.Value))
            {
                var start = _position;
                var buff = ConsumeNumber();
                tokens.Add(new Token("number", int.Parse(buff), start, start + buff.Length));
            }
            else if (_current.Value == '-')
            {
                var start = _position;
                var buff = ConsumeNumber();
                if (buff.Length > 1)
                {
                    tokens.Add(new Token("number", int.Parse(buff), start, start + buff.Length));
                }
                else
                {
                    throw new LexerException(start, buff, $"Unknown token '{buff}'");
                }
            }
            else if (_current.Value == '"')
            {
                tokens.Add(ConsumeQuotedIdentifier());
            }
            else if (_current.Value == '<')
            {
                tokens.Add(MatchOrElse('=', "lte", "lt"));
            }
            else if (_current.Value == '>')
            {
                tokens.Add(MatchOrElse('=', "gte", "gt"));
            }
            else if (_current.Value == '!')
            {
                tokens.Add(MatchOrElse('=', "ne", "not"));
            }
            else if (_current.Value == '=')
            {
                if (Next() == '=')
                {
                    tokens.Add(new Token("eq", "==", _position - 1, _position + 1));
                    Next();
                }
                else
                {
                    var position = _current.HasValue ? _position - 1 : _position;
                    throw new LexerException(position, "=", "Unknown token '='");
                }
            }
            else
            {
                throw new LexerException(_position, _current.Value.ToString(),
                    $"Unknown token {_current.Value}");
            }
        }

        tokens.Add(new Token("eof", string.Empty, _length, _length));
        return tokens;
    }

    private void InitializeForExpression(string expression)
    {
        if (string.IsNullOrEmpty(expression))
        {
            throw new EmptyExpressionException();
        }
        _position = 0;
        _expression = expression;
        _chars = expression.ToCharArray();
        _current = _chars[_position];
        _length = expression.Length;
    }

    private char? Next()
    {
        if (_position == _length - 1)
        {
            _current = null;
        }
        else
        {
            _position++;
            _current = _chars[_position];
        }
        return _current;
    }

    private string ConsumeNumber()
    {
        var start = _position;
        var buff = new StringBuilder();
        buff.Append(_current!.Value);
        while (Next().HasValue && ValidNumber.Contains(_current.Value))
        {
            buff.Append(_current.Value);
        }
        return buff.ToString();
    }

    private string ConsumeUntil(char delimiter)
    {
        var start = _position;
        var buff = new StringBuilder();
        Next();

        while (_current != delimiter)
        {
            if (_current == '\\')
            {
                buff.Append('\\');
                Next();
            }
            if (!_current.HasValue)
            {
                throw new LexerException(start, _expression[start..],
                    $"Unclosed {delimiter} delimiter");
            }
            buff.Append(_current.Value);
            Next();
        }

        Next(); // Skip the closing delimiter
        return buff.ToString();
    }

    private Token ConsumeLiteral()
    {
        var start = _position;
        var lexeme = ConsumeUntil('`').Replace("\\`", "`");

        object? parsedJson;
        try
        {
            parsedJson = JsonSerializer.Deserialize<object>(lexeme);
        }
        catch
        {
            // Try as quoted string
            try
            {
                parsedJson = JsonSerializer.Deserialize<object>($"\"{lexeme.TrimStart()}\"");
            }
            catch
            {
                throw new LexerException(start, _expression[start..], $"Bad token {lexeme}");
            }
        }

        var tokenLen = _position - start;
        return new Token("literal", parsedJson, start, tokenLen);
    }

    private Token ConsumeQuotedIdentifier()
    {
        var start = _position;
        var lexeme = "\"" + ConsumeUntil('"') + "\"";

        try
        {
            var value = JsonSerializer.Deserialize<string>(lexeme);
            var tokenLen = _position - start;
            return new Token("quoted_identifier", value, start, tokenLen);
        }
        catch (Exception e)
        {
            var errorMessage = e.Message.Split(':')[0];
            throw new LexerException(start, lexeme, errorMessage);
        }
    }

    private Token ConsumeRawStringLiteral()
    {
        var start = _position;
        var lexeme = ConsumeUntil('\'').Replace("\\'", "'");
        var tokenLen = _position - start;
        return new Token("literal", lexeme, start, tokenLen);
    }

    private Token MatchOrElse(char expected, string matchType, string elseType)
    {
        var start = _position;
        var current = _current!.Value;
        var nextChar = Next();

        if (nextChar == expected)
        {
            Next();
            return new Token(matchType, current.ToString() + expected, start, start + 2);
        }

        return new Token(elseType, current.ToString(), start, start + 1);
    }
}
