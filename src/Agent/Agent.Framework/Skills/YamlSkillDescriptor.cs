// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Framework.Skills;

/// <summary>
/// YAML descriptor for skill metadata files
/// </summary>
public class YamlSkillDescriptor
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "tools")]
    public List<string> Tools { get; set; } = [];

    public static YamlSkillDescriptor FromYaml(string yamlContent)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        try
        {
            return deserializer.Deserialize<YamlSkillDescriptor>(yamlContent);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to deserialize skill metadata YAML: {ex.Message}", ex);
        }
    }
}
