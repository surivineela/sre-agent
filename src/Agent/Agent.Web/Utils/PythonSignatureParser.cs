// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using Agent.Framework;

namespace Agent.Web.Utils;

/// <summary>
/// Parses Python function signatures to extract parameter information
/// </summary>
public static class PythonSignatureParser
{
    /// <summary>
    /// Extracts parameters from a Python function's 'def main(...)' signature
    /// </summary>
    /// <param name="functionCode">The Python function code</param>
    /// <returns>List of parameters with name, type, required status, and description</returns>
    public static List<YamlParameter> ExtractParameters(string functionCode)
    {
        var parameters = new List<YamlParameter>();

        if (string.IsNullOrWhiteSpace(functionCode))
        {
            return parameters;
        }

        // Match: def main(param1: str, param2: int = 10, param3: Optional[str] = None)
        // Handles multi-line signatures
        var mainFunctionMatch = Regex.Match(
            functionCode,
            @"def\s+main\s*\((.*?)\)\s*(?:->.*?)?:",
            RegexOptions.Singleline | RegexOptions.IgnoreCase
        );

        if (!mainFunctionMatch.Success)
        {
            return parameters;
        }

        var paramString = mainFunctionMatch.Groups[1].Value.Trim();
        if (string.IsNullOrWhiteSpace(paramString))
        {
            return parameters; // No parameters
        }

        // Split by comma, but handle nested types like Dict[str, int]
        var paramParts = SplitParameters(paramString);

        foreach (var part in paramParts)
        {
            var param = ParseParameter(part.Trim());
            if (param != null)
            {
                parameters.Add(param);
            }
        }

        return parameters;
    }

    /// <summary>
    /// Splits parameter string by comma, respecting brackets
    /// </summary>
    private static List<string> SplitParameters(string paramString)
    {
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        var bracketDepth = 0;

        foreach (var ch in paramString)
        {
            if (ch == '[' || ch == '{' || ch == '(')
            {
                bracketDepth++;
                current.Append(ch);
            }
            else if (ch == ']' || ch == '}' || ch == ')')
            {
                bracketDepth--;
                current.Append(ch);
            }
            else if (ch == ',' && bracketDepth == 0)
            {
                parts.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString());
        }

        return parts;
    }

    /// <summary>
    /// Parses a single parameter definition
    /// Examples:
    /// - "url: str"
    /// - "timeout: int = 30"
    /// - "enabled: bool = True"
    /// - "data: Optional[Dict[str, Any]] = None"
    /// </summary>
    private static YamlParameter? ParseParameter(string paramDef)
    {
        if (string.IsNullOrWhiteSpace(paramDef))
        {
            return null;
        }

        // Pattern: name: type = default
        var match = Regex.Match(
            paramDef,
            @"^\s*(\w+)\s*(?::\s*(.+?))?\s*(?:=\s*(.+))?\s*$"
        );

        if (!match.Success)
        {
            return null;
        }

        var name = match.Groups[1].Value.Trim();
        var typeHint = match.Groups[2].Success ? match.Groups[2].Value.Trim() : "";
        var defaultValue = match.Groups[3].Success ? match.Groups[3].Value.Trim() : null;

        // Determine if required (no default value)
        var required = string.IsNullOrEmpty(defaultValue);

        // Map Python type to parameter type
        var paramType = MapPythonType(typeHint);

        // Generate description from type hint
        var description = GenerateDescription(name, typeHint, defaultValue);

        return new YamlParameter
        {
            Name = name,
            Type = paramType,
            Description = description,
            Required = required
        };
    }

    /// <summary>
    /// Maps Python type hints to parameter types
    /// </summary>
    private static string MapPythonType(string typeHint)
    {
        if (string.IsNullOrWhiteSpace(typeHint))
        {
            return "string"; // Default
        }

        // Normalize type hint (remove Optional, whitespace)
        var normalized = typeHint
            .Replace("Optional[", "")
            .Replace("]", "")
            .Replace(" ", "")
            .ToLowerInvariant();

        // Check for common types
        if (normalized.Contains("int"))
        {
            return "int";
        }
        if (normalized.Contains("float") || normalized.Contains("double"))
        {
            return "double";
        }
        if (normalized.Contains("bool"))
        {
            return "bool";
        }
        if (normalized.Contains("str") || normalized.Contains("string"))
        {
            return "string";
        }
        if (normalized.Contains("dict") || normalized.Contains("object"))
        {
            return "object";
        }
        if (normalized.Contains("list") || normalized.Contains("array"))
        {
            return "array";
        }

        // Default to string for unknown types
        return "string";
    }

    /// <summary>
    /// Generates a human-readable description from parameter information
    /// </summary>
    private static string GenerateDescription(string name, string typeHint, string? defaultValue)
    {
        var parts = new List<string>();

        // Convert snake_case to readable words
        var readableName = ConvertSnakeCaseToWords(name);

        // Add type information
        if (!string.IsNullOrWhiteSpace(typeHint))
        {
            var cleanType = typeHint.Replace("Optional[", "").Replace("]", "").Trim();
            parts.Add($"{readableName} ({cleanType})");
        }
        else
        {
            parts.Add(readableName);
        }

        // Add default value info
        if (!string.IsNullOrEmpty(defaultValue) && defaultValue != "None")
        {
            parts.Add($"default: {defaultValue}");
        }

        return string.Join(" - ", parts);
    }

    /// <summary>
    /// Converts snake_case to readable words
    /// Example: "max_retries" -> "Max retries"
    /// </summary>
    private static string ConvertSnakeCaseToWords(string snakeCase)
    {
        if (string.IsNullOrWhiteSpace(snakeCase))
        {
            return snakeCase;
        }

        var words = snakeCase.Split('_');
        var capitalized = words.Select((w, i) =>
            i == 0 ? char.ToUpper(w[0]) + w.Substring(1) : w
        );

        return string.Join(" ", capitalized);
    }
}
