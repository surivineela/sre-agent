// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Core.Helpers.ExtendedAgents;

public static class AgentYamlParser
{
    public static IAgentDescriptor? ParseAgentYaml(string yaml)
    {
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var yamlDict = deserializer.Deserialize<Dictionary<string, object>>(yaml) ?? throw new InvalidOperationException("YAML content is empty or invalid");

            if (yamlDict.TryGetValue("kind", out var kindObj) && kindObj?.ToString() == "AgentConfiguration")
            {
                if (!yamlDict.TryGetValue("spec", out var specObj))
                {
                    throw new InvalidOperationException("AgentConfiguration must have spec structure");
                }

                Dictionary<string, object> spec = specObj switch
                {
                    Dictionary<string, object> d => d,
                    Dictionary<object, object> o => o.ToDictionary(k => k.Key?.ToString() ?? string.Empty, v => v.Value),
                    _ => throw new InvalidOperationException($"AgentConfiguration spec must be an object. Got type: {specObj?.GetType().FullName ?? "null"}")
                };
                var serializer = new SerializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build();
                var agentYaml = serializer.Serialize(spec);
                return YamlAgentDescriptor.FromYaml(agentYaml);
            }
            return YamlAgentDescriptor.FromYaml(yaml);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse YAML into AgentDescriptor: {ex.Message}", ex);
        }
    }

    public static List<string> ExtractToolNames(string yaml)
    {
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            var yamlDict = deserializer.Deserialize<Dictionary<string, object>>(yaml);
            if (yamlDict == null) return new List<string>();
            if (yamlDict.TryGetValue("kind", out var kindObj) && kindObj?.ToString() == "AgentConfiguration")
            {
                if (yamlDict.TryGetValue("spec", out var specObj) &&
                    specObj is Dictionary<string, object> spec &&
                    spec.TryGetValue("tools", out var toolsObj) &&
                    toolsObj is List<object> toolsList)
                {
                    return toolsList.Cast<string>().ToList();
                }
            }
            else if (yamlDict.TryGetValue("tools", out var flatToolsObj) && flatToolsObj is List<object> flatToolsList)
            {
                return flatToolsList.Cast<string>().ToList();
            }
            return new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public static bool IsKubernetesFormat(string yaml)
    {
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var yamlDict = deserializer.Deserialize<Dictionary<string, object>>(yaml);

            return yamlDict != null &&
                   yamlDict.TryGetValue("kind", out var kindObj) &&
                   kindObj?.ToString() == "AgentConfiguration" &&
                   yamlDict.ContainsKey("spec");
        }
        catch
        {
            return false;
        }
    }
}
