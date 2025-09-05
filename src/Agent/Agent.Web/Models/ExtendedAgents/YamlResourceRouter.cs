// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Tools;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Web.Models.ExtendedAgents;

public class YamlResourceRouter
{
    public static object? DeserializeResource(string kind, string yaml)
    {
        var toolTypeMappings = new Dictionary<string, Type>
        {
            ["KustoTool"] = typeof(KustoToolApiModel),

        };

        var connectorTypeMappings = new Dictionary<string, Type>
        {
            ["Kusto"] = typeof(KustoConnectorApiModel),


        };

        var yamlDeserializer = new DeserializerBuilder()
                   .WithNamingConvention(UnderscoredNamingConvention.Instance)
                       .WithTypeConverter(new PolymorphicTypeConverter<ExtendedAgentToolApiModel>(toolTypeMappings))
                       .WithTypeConverter(new PolymorphicTypeConverter<ExtendedAgentConnectorApiModel>(connectorTypeMappings))
                       .Build();

        return kind switch
        {
            "AgentConfiguration" => yamlDeserializer.Deserialize<AgentDeploymentModel>(yaml),
            "ToolList" => yamlDeserializer.Deserialize<ToolsDeploymentModel>(yaml),
            "ConnectorList" => yamlDeserializer.Deserialize<ConnectorsDeploymentModel>(yaml),
            "CommonToolsList" => yamlDeserializer.Deserialize<CommonToolsListDeploymentModel>(yaml),
            "CommonPrompt" => yamlDeserializer.Deserialize<CommonPromptDeploymentModel>(yaml),
            "PluginConfiguration" => yamlDeserializer.Deserialize<PluginConfigDeploymentModel>(yaml),
            _ => throw new NotSupportedException($"Unsupported kind: {kind}")
        };
    }
}
