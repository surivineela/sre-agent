// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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

    [YamlMember(Alias = "connector")]
    public string Connector { get; set; } = string.Empty;

    [YamlMember(Alias = "description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "parameters")]
    public List<YamlParameter> Parameters { get; set; } = new();

    public List<string> Attributes { get; set; } = new();

    public abstract void Validate();

    public virtual T GetConnector<T>() where T : DataConnectorDefinitionBase, new()
    {
        if (string.IsNullOrWhiteSpace(Connector))
            throw new InvalidOperationException("Tool definition or connector name is missing.");

        string connectorName = Connector.Trim();
        string connectorPath = Path.Combine(AppContext.BaseDirectory, "Connectors", $"{connectorName}.yaml");

        if (!System.IO.File.Exists(connectorPath))
            throw new FileNotFoundException($"Connector YAML file not found at: {connectorPath}");

        try
        {
            var yamlContent = System.IO.File.ReadAllText(connectorPath);

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var connector = deserializer.Deserialize<T>(yamlContent);

            connector.Validate(); // ensure fields like ClusterUrl and Database are set

            return connector;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load or parse connector file '{connectorName}.yaml'.", ex);
        }
    }

}
