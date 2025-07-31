// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

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

    // New: Parsed dictionary info
    public bool IsDictionaryTarget =>
        Target.StartsWith("dictionary:", StringComparison.OrdinalIgnoreCase);

    public (string DictName, string ValueType)? GetDictionaryTargetInfo()
    {
        if (!IsDictionaryTarget) return null;
        var parts = Target.Split(':');
        if (parts.Length < 3) return (parts[1], "string"); // default to string
        return (parts[1], parts[2]);
    }
}
