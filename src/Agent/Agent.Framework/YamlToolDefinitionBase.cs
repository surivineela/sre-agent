// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Reasoning.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Framework;

/// <summary>
/// Represents the structure of a tool definition within a YAML file.
/// </summary>
public abstract class YamlToolDefinitionBase
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "type")]
    public string Type { get; set; } = string.Empty;

    [YamlMember(Alias = "connector")]
    public string Connector { get; set; } = string.Empty;

    [YamlIgnore]
    public DataConnectorDefinitionBase? ConnectorData { get; set; }

    [YamlMember(Alias = "description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "parameters")]
    public List<YamlParameter> Parameters { get; set; } = new();
    [YamlMember(Alias = "attributes")]
    public List<string> Attributes { get; set; } = new();

    [YamlMember(Alias = "metadata")]
    public YamlMetadata Metadata { get; set; } = new();
    
    public abstract void Validate();


}
