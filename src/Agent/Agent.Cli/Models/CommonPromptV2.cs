// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
    /// <summary>
    /// Common prompt specification for YAML configurations (V2).
    /// Contains a single prompt property for common prompt definitions.
    /// </summary>
    public class CommonPromptSpecV2
    {
        [YamlMember(Alias = "prompt", ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
        public string? Prompt { get; set; }
    }

    /// <summary>
    /// CLI YAML wrapper for common prompt configurations.
    /// Adds YAML envelope fields (api_version, kind) for file serialization.
    /// </summary>
    public class CommonPromptV2 : ResourceModel
    {
        public CommonPromptV2()
        {
            ApiVersion = YamlApiVersion.V2;
            Kind = ResourceKind.CommonPromptV2;
        }

        /// <summary>
        /// Resource metadata (owner, tags, version, timestamps).
        /// </summary>
        [YamlMember(Alias = "metadata", Order = 0)]
        public ResourceMetadataModel Metadata { get; set; } = new();

        /// <summary>
        /// Common prompt specification properties.
        /// </summary>
        [YamlMember(Alias = "spec", Order = 1)]
        public CommonPromptSpecV2 Spec { get; set; } = new();

        /// <summary>
        /// Normalizes string properties to ensure clean YAML literal block formatting.
        /// Removes trailing whitespace from the prompt field.
        /// </summary>
        public override void Normalize()
        {
            if (Spec != null)
            {
                Spec.Prompt = NormalizeString(Spec.Prompt);
            }
        }

        /// <summary>
        /// Parses a YAML string into a CommonPromptV2 object.
        /// </summary>
        /// <param name="yaml">The YAML string to parse</param>
        /// <returns>The parsed CommonPromptV2 object</returns>
        public static CommonPromptV2 ParseYaml(string yaml)
        {
            var deserializer = GetDeserializerBuilder().Build();
            return deserializer.Deserialize<CommonPromptV2>(yaml);
        }

        /// <summary>
        /// Loads a YAML file and parses it into a CommonPromptV2 object asynchronously.
        /// </summary>
        /// <param name="fileName">The file path to read and parse</param>
        /// <returns>The parsed CommonPromptV2 object, or null if the file doesn't exist or parsing fails</returns>
        public static async Task<CommonPromptV2?> LoadYamlAsync(string fileName)
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
}
