// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Framework.Reasoning.Models;

// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

/// <summary>
/// Base class for all YAML-defined data connectors (Kusto, SQL, API, etc.)
/// </summary>
public class DataConnectorDefinitionBase
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "enabled")]
    public bool Enabled { get; set; } = false;

    [YamlMember(Alias = "type")]
    public string Type { get; set; } = string.Empty;

    [YamlMember(Alias = "description")]
    public string? Description { get; set; }

    [YamlMember(Alias = "auth")]
    public ConnectorAuthSettings Auth { get; set; } = new();

    [YamlMember(Alias = "metadata")]
    public YamlMetadata Metadata { get; set; } = new();

    public virtual void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Name cannot be null or empty.", nameof(Name));
        }
        if (string.IsNullOrWhiteSpace(Type))
        {
            throw new ArgumentException("Type cannot be null or empty.", nameof(Type));
        }
    }
}
