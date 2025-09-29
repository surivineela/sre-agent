// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Reasoning.Models;
using YamlDotNet.Serialization;

namespace Agent.Framework;

/// <summary>
/// Represents the structure of a tool definition within a YAML file.
/// </summary>
public class YamlPluginConfig
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "type")]
    public string Type { get; set; } = string.Empty;

    [YamlMember(Alias = "config")]
    public Dictionary<string, object> Config = new();

    [YamlMember(Alias = "metadata")]
    public YamlMetadata Metadata { get; set; } = new();

    public virtual void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Name is required.");
        }

        if (Config == null || Config.Count == 0)
        {
            throw new ArgumentException("Config is required.");
        }
    }
}
