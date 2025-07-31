using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Agent.Cli.Models;
using Agent.Framework;

namespace Agent.Cli.Helpers;

/// <summary>
/// Helper class for YAML serialization and deserialization operations.
/// </summary>
public static class YamlHelper
{
    /// <summary>
    /// Writes a dictionary object to a YAML file using camelCase naming convention.
    /// </summary>
    /// <param name="folder">The target folder path</param>
    /// <param name="name">The file name (without extension)</param>
    /// <param name="data">The data to serialize</param>
    public static void WriteYamlFile(string folder, string name, Dictionary<string, object> data)
    {
        Directory.CreateDirectory(folder);
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var yaml = serializer.Serialize(data);
        File.WriteAllText(Path.Combine(folder, $"{name}.yaml"), yaml, Encoding.UTF8);
    }

    /// <summary>
    /// Writes a YamlAgentDescriptor to a YAML file using underscored naming convention.
    /// </summary>
    /// <param name="folder">The target folder path</param>
    /// <param name="name">The file name (without extension)</param>
    /// <param name="agent">The agent descriptor to serialize</param>
    public static void WriteAgentYamlFile(string folder, string name, YamlAgentDescriptor agent)
    {
        Directory.CreateDirectory(folder);
        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        var yaml = serializer.Serialize(agent);
        File.WriteAllText(Path.Combine(folder, $"{name}.yaml"), yaml, Encoding.UTF8);
    }

    /// <summary>
    /// Creates a deserializer with camelCase naming convention for reading YAML files.
    /// </summary>
    /// <returns>A configured YAML deserializer</returns>
    public static IDeserializer CreateCamelCaseDeserializer()
    {
        return new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }
}
