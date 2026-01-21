// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Framework.Skills;

/// <summary>
/// Nested metadata containing api_version and kind for CLI compatibility.
/// </summary>
public class YamlSkillMetadata
{
    [YamlMember(Alias = "api_version")]
    public string? ApiVersion { get; set; }

    [YamlMember(Alias = "kind")]
    public string? Kind { get; set; }
}

/// <summary>
/// YAML descriptor for skill metadata files
/// </summary>
public class YamlSkillDescriptor
{
    /// <summary>
    /// Nested metadata containing api_version and kind for CLI compatibility. Optional for runtime.
    /// </summary>
    [YamlMember(Alias = "metadata")]
    public YamlSkillMetadata? Metadata { get; set; }

    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "tools")]
    public List<string> Tools { get; set; } = [];

    [YamlMember(Alias = "first_party_only")]
    public bool FirstPartyOnly { get; set; } = false;

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
