// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
    /// <summary>
    /// CLI YAML wrapper for agent configurations.
    /// Adds YAML envelope fields (api_version, kind) for file serialization.
    /// </summary>
    public class ExtendedAgentV1 : ResourceModel
    {
        public ExtendedAgentV1()
        {
            ApiVersion = YamlApiVersion.V1;
            Kind = "AgentConfiguration";
        }

        /// <summary>
        /// Resource metadata (owner, tags, version, timestamps).
        /// </summary>
        [YamlMember(Alias = "metadata", Order = 0)]
        public ResourceMetadataModel Metadata { get; set; } = new();

        /// <summary>
        /// Agent specification properties.
        /// </summary>
        [YamlMember(Alias = "spec")]
        public ExtendedAgentSpecV1 Spec { get; set; } = new();

        /// <summary>
        /// Parses a YAML string into an ExtendedAgentV1 object.
        /// </summary>
        /// <param name="yaml">The YAML string to parse</param>
        /// <returns>The parsed ExtendedAgentV1 object</returns>
        public static ExtendedAgentV1 ParseYaml(string yaml)
        {
            var deserializer = GetDeserializerBuilder().Build();
            return deserializer.Deserialize<ExtendedAgentV1>(yaml);
        }

        /// <summary>
        /// Loads a YAML file and parses it into an ExtendedAgentV1 object asynchronously.
        /// </summary>
        /// <param name="fileName">The file path to read and parse</param>
        /// <returns>The parsed ExtendedAgentV1 object, or null if the file doesn't exist or parsing fails</returns>
        public static async Task<ExtendedAgentV1?> LoadYamlAsync(string fileName)
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
