// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Generic;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;

/// <summary>
/// Represents a single parameter within a YAML tool definition.
/// </summary>
public sealed class YamlParameter
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "type")]
    public string Type { get; set; } = string.Empty;

    [YamlMember(Alias = "description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "map_to")]
    public string MapTo { get; set; } = string.Empty; // default to Name if null

    [YamlMember(Alias = "required")]
    public bool Required { get; set; } = false;

    [YamlMember(Alias = "target")]
    public string Target { get; set; } = "direct"; // "direct", "dictionary:name", "array:name", "ignored"

    /// <summary>
    /// If a value is provided, this parameter is "baked in" to the function call.
    /// If null, it's expected to be provided at invocation time.
    /// </summary>
    [YamlMember(Alias = "value")]
    public object? Value { get; set; }

    [YamlMember(Alias = "validation")]
    public YamlParameterValidation? Validation { get; set; }

    // New: Parsed dictionary info
    public bool IsDictionaryTarget =>
        Target?.StartsWith("dictionary:", StringComparison.OrdinalIgnoreCase) ?? false;

    public (string DictName, string ValueType)? GetDictionaryTargetInfo()
    {
        if (!IsDictionaryTarget || Target == null) return null;
        var parts = Target.Split(':');
        if (parts.Length < 3) return (parts[1], "string"); // default to string
        return (parts[1], parts[2]);
    }
}

public sealed class YamlParameterValidation
{
    [YamlMember(Alias = "regex")]
    public string? Regex { get; set; }

    [YamlMember(Alias = "error_message")]
    public string? ErrorMessage { get; set; }

    [YamlMember(Alias = "normalize")]
    public List<string>? Normalize { get; set; }

    public bool HasRegex => !string.IsNullOrWhiteSpace(Regex);

    [YamlIgnore]
    private Regex? _cachedRegex;

    [YamlIgnore]
    private string? _cachedPattern;

    public Regex? BuildRegex()
    {
        if (!HasRegex)
        {
            return null;
        }

        if (_cachedRegex == null || !string.Equals(_cachedPattern, Regex, StringComparison.Ordinal))
        {
            _cachedRegex = new Regex(Regex!, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            _cachedPattern = Regex;
        }

        return _cachedRegex;
    }
}
