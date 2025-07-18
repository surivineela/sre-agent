// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Runtime.Reasoning.Models;

/// <summary>
/// Represents the structure of a tool definition within a YAML file.
/// </summary>
public abstract class YamlToolDefinitionBase
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "type")]
    public string Type { get; set; } = string.Empty;

    [YamlMember(Alias = "description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "parameters")]
    public List<YamlParameter> Parameters { get; set; } = new();

    public List<string> Attributes { get; set; } = new();

    public abstract void Validate();
}
