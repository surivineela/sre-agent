// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

using System;

namespace Agent.Core.Helpers.JmesPath;

/// <summary>
/// Base exception for all JMESPath errors.
/// </summary>
public class JmesPathException : Exception
{
    public JmesPathException() { }
    public JmesPathException(string message) : base(message) { }
    public JmesPathException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when parsing fails.
/// </summary>
public class ParseException : JmesPathException
{
    public int LexPosition { get; set; }
    public string? TokenValue { get; set; }
    public string? TokenType { get; set; }
    public string? Expression { get; set; }

    public ParseException(int lexPosition, string? tokenValue, string? tokenType, string message = "Invalid jmespath expression")
        : base(message)
    {
        LexPosition = lexPosition;
        TokenValue = tokenValue;
        TokenType = tokenType?.ToUpperInvariant();
    }

    public override string ToString()
    {
        if (Expression != null)
        {
            var underline = new string(' ', LexPosition + 1) + "^";
            return $"{Message}: Parse error at column {LexPosition}, " +
                   $"token \"{TokenValue}\" ({TokenType}), for expression:\n\"{Expression}\"\n{underline}";
        }
        return base.ToString();
    }
}

/// <summary>
/// Exception thrown when the expression is incomplete.
/// </summary>
public class IncompleteExpressionException : ParseException
{
    public IncompleteExpressionException(int lexPosition, string? tokenValue, string? tokenType)
        : base(lexPosition, tokenValue, tokenType, "Invalid jmespath expression: Incomplete expression")
    {
    }

    public void SetExpression(string expression)
    {
        Expression = expression;
        LexPosition = expression.Length;
        TokenType = null;
        TokenValue = null;
    }

    public override string ToString()
    {
        if (Expression != null)
        {
            var underline = new string(' ', LexPosition + 1) + "^";
            return $"Invalid jmespath expression: Incomplete expression:\n\"{Expression}\"\n{underline}";
        }
        return base.ToString();
    }
}

/// <summary>
/// Exception thrown when lexer encounters an error.
/// </summary>
public class LexerException : ParseException
{
    public LexerException(int lexerPosition, string? lexerValue, string message, string? expression = null)
        : base(lexerPosition, lexerValue, "lexer_error", message)
    {
        Expression = expression;
    }

    public override string ToString()
    {
        if (Expression != null)
        {
            var underline = new string(' ', LexPosition) + "^";
            return $"Bad jmespath expression: {Message}:\n{Expression}\n{underline}";
        }
        return $"Bad jmespath expression: {Message}";
    }
}

/// <summary>
/// Exception thrown when function arity doesn't match.
/// </summary>
public class ArityException : ParseException
{
    public int ExpectedArity { get; }
    public int ActualArity { get; }
    public string FunctionName { get; }

    public ArityException(int expected, int actual, string functionName)
        : base(0, null, null, BuildMessage(expected, actual, functionName))
    {
        ExpectedArity = expected;
        ActualArity = actual;
        FunctionName = functionName;
    }

    private static string BuildMessage(int expected, int actual, string functionName)
    {
        var word = expected == 1 ? "argument" : "arguments";
        return $"Expected {expected} {word} for function {functionName}(), received {actual}";
    }
}

/// <summary>
/// Exception thrown when variadic function arity doesn't match minimum.
/// </summary>
public class VariadicArityException : ArityException
{
    public VariadicArityException(int expected, int actual, string functionName)
        : base(expected, actual, functionName)
    {
    }

    public override string ToString()
    {
        var word = ExpectedArity == 1 ? "argument" : "arguments";
        return $"Expected at least {ExpectedArity} {word} for function {FunctionName}(), received {ActualArity}";
    }
}

/// <summary>
/// Exception thrown when function receives wrong type.
/// </summary>
public class JmesPathTypeException : JmesPathException
{
    public string FunctionName { get; }
    public object? CurrentValue { get; }
    public string ActualType { get; }
    public string[] ExpectedTypes { get; }

    public JmesPathTypeException(string functionName, object? currentValue, string actualType, string[] expectedTypes)
        : base(BuildMessage(functionName, currentValue, actualType, expectedTypes))
    {
        FunctionName = functionName;
        CurrentValue = currentValue;
        ActualType = actualType;
        ExpectedTypes = expectedTypes;
    }

    private static string BuildMessage(string functionName, object? currentValue, string actualType, string[] expectedTypes)
    {
        var expectedTypesStr = string.Join(", ", expectedTypes);
        return $"In function {functionName}(), invalid type for value: {currentValue}, " +
               $"expected one of: [{expectedTypesStr}], received: \"{actualType}\"";
    }
}

/// <summary>
/// Exception thrown when expression is empty.
/// </summary>
public class EmptyExpressionException : JmesPathException
{
    public EmptyExpressionException()
        : base("Invalid JMESPath expression: cannot be empty.")
    {
    }
}

/// <summary>
/// Exception thrown when function is unknown.
/// </summary>
public class UnknownFunctionException : JmesPathException
{
    public UnknownFunctionException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when an invalid value is encountered.
/// </summary>
public class InvalidValueException : JmesPathException
{
    public InvalidValueException(string message) : base(message) { }
}
