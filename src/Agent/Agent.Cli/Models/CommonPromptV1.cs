// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
    /// <summary>
    /// CLI YAML wrapper for common prompt configurations (V1 format).
    /// Represents the legacy format with api_version: agent.platform.ai/v1.
    /// </summary>
    public class CommonPromptV1 : ResourceModel
    {
        public CommonPromptV1()
        {
            ApiVersion = "agent.platform.ai/v1";
            Kind = "CommonPrompt";
        }

        /// <summary>
        /// Resource metadata (owner, tags, version, timestamps).
        /// </summary>
        [YamlMember(Alias = "metadata", Order = 0)]
        public ResourceMetadataModel? Metadata { get; set; }

        /// <summary>
        /// Common prompt specification properties.
        /// </summary>
        [YamlMember(Alias = "spec", Order = 1)]
        public CommonPromptSpecV1 Spec { get; set; } = new();

        /// <summary>
        /// Parses a YAML string into a CommonPromptV1 object.
        /// </summary>
        /// <param name="yaml">The YAML string to parse</param>
        /// <returns>The parsed CommonPromptV1 object</returns>
        public static CommonPromptV1 ParseYaml(string yaml)
        {
            var deserializer = GetDeserializerBuilder().Build();
            return deserializer.Deserialize<CommonPromptV1>(yaml);
        }

        /// <summary>
        /// Loads a YAML file and parses it into a CommonPromptV1 object asynchronously.
        /// </summary>
        /// <param name="fileName">The file path to read and parse</param>
        /// <returns>The parsed CommonPromptV1 object, or null if the file doesn't exist or parsing fails</returns>
        public static async Task<CommonPromptV1?> LoadYamlAsync(string fileName)
        {
            try
            {
                if (!File.Exists(fileName))
                {
                    return null;
                }

                var yamlContent = await File.ReadAllTextAsync(fileName);
                return ParseYaml(yamlContent);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Common prompt specification for YAML configurations (V1).
    /// Contains a list of common prompts with name and prompt properties.
    /// </summary>
    public class CommonPromptSpecV1
    {
        [YamlMember(Alias = "common_prompts")]
        public List<CommonPromptItemV1>? CommonPrompts { get; set; }
    }

    /// <summary>
    /// Individual common prompt item in V1 format.
    /// </summary>
    public class CommonPromptItemV1
    {
        [YamlMember(Alias = "name")]
        public string? Name { get; set; }

        [YamlMember(Alias = "prompt", ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
        public string? Prompt { get; set; }
    }
}
