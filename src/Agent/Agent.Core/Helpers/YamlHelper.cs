// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Core.Helpers
{
    public static class YamlHelper
    {
        public static string Serialize<T>(T obj)
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            return serializer.Serialize(obj);
        }

        public static T Deserialize<T>(string yaml)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            return deserializer.Deserialize<T>(yaml);
        }

        public static List<YamlAgentDescriptor> LoadAgentsFromYamlDirectories(List<string> yamlDirectories, string? prefix = null)
        {
            List<YamlAgentDescriptor> agents = new();
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            foreach (var directory in yamlDirectories)
            {
                string fullDir = Path.Combine(baseDir, directory);
                if (Directory.Exists(fullDir))
                {
                    var yamlFiles = Directory.GetFiles(fullDir, "*.yaml", SearchOption.AllDirectories)
                                   .Concat(Directory.GetFiles(fullDir, "*.yml", SearchOption.AllDirectories));
                    foreach (var yamlFile in yamlFiles)
                    {
                        var filename = Path.GetFileName(yamlFile);
                        if (prefix != null && !filename.StartsWith(prefix))
                        {
                            continue; // Skip files that do not match the prefix
                        }
                        var agentDescriptor = LoadAgentFromYaml(yamlFile);
                        agents.Add(agentDescriptor);
                    }
                }
            }
            return agents;
        }

        public static YamlAgentDescriptor LoadAgentFromYaml(string yamlFilePath)
        {
            try
            {
                if (!File.Exists(yamlFilePath))
                    throw new FileNotFoundException("YAML file not found", yamlFilePath);

                string yamlContent = File.ReadAllText(yamlFilePath);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .Build();
                var agentDescriptor = deserializer.Deserialize<YamlAgentDescriptor>(yamlContent);
                return agentDescriptor;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to parse YAML into AgentDescriptor", ex);
            }
        }
    }
}

