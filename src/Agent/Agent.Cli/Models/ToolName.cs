// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Cli.Models;

/// <summary>
/// Represents a strongly-typed tool or query name with implicit string conversion.
/// Provides type safety for commonly used tool names while maintaining string compatibility.
/// </summary>
public sealed class ToolName : IEquatable<ToolName>, IEquatable<string>
{
    private readonly string _value;

    // Private constructor - use factory methods or implicit conversion
    private ToolName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Tool name cannot be null or whitespace.", nameof(value));
        }
        _value = value;
    }

    // Well-known tool names as static properties
    public static ToolName KustoTool => new("KustoTool");
    public static ToolName LinkTool => new("LinkTool");
    public static ToolName PythonTool => new("PythonTool");
    public static ToolName KustoQuery => new("KustoQuery");

    // Implicit conversion from string to ToolName
    public static implicit operator ToolName(string value) => new(value);

    // Implicit conversion from ToolName to string
    public static implicit operator string(ToolName toolName) => toolName._value;

    // Equality operators (case-insensitive)
    public static bool operator ==(ToolName? left, ToolName? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return string.Equals(left._value, right._value, StringComparison.OrdinalIgnoreCase);
    }

    public static bool operator !=(ToolName? left, ToolName? right) => !(left == right);

    public static bool operator ==(ToolName? left, string? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return string.Equals(left._value, right, StringComparison.OrdinalIgnoreCase);
    }

    public static bool operator !=(ToolName? left, string? right) => !(left == right);

    // IEquatable implementations (case-insensitive)
    public bool Equals(ToolName? other)
    {
        if (other is null) return false;
        return string.Equals(_value, other._value, StringComparison.OrdinalIgnoreCase);
    }

    public bool Equals(string? other)
    {
        if (other is null) return false;
        return string.Equals(_value, other, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        return obj switch
        {
            ToolName toolName => Equals(toolName),
            string str => Equals(str),
            _ => false
        };
    }

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(_value);

    public override string ToString() => _value;

    // Factory method for creating custom tool names
    public static ToolName Create(string value) => new(value);

    // Helper method to check if a string matches a known tool name
    public static bool IsKnownTool(string value)
    {
        return value switch
        {
            var v when string.Equals(v, "KustoTool", StringComparison.OrdinalIgnoreCase) => true,
            var v when string.Equals(v, "LinkTool", StringComparison.OrdinalIgnoreCase) => true,
            var v when string.Equals(v, "PythonTool", StringComparison.OrdinalIgnoreCase) => true,
            var v when string.Equals(v, "KustoQuery", StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        };
    }
}
