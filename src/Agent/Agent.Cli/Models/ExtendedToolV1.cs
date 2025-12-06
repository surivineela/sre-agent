// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
    public class ExtendedToolV1 : ResourceModel
    {
        [YamlMember(Alias = "name")]
        public string? Name { get; set; }

        [YamlMember(Alias = "metadata", Order = 0)]
        public ResourceMetadataModel? Metadata { get; set; }

        [YamlMember(Alias = "type")]
        public string? Type { get; set; }

        [YamlMember(Alias = "connector")]
        public string? Connector { get; set; }

        [YamlMember(Alias = "description")]
        public string? Description { get; set; }

        [YamlMember(Alias = "parameters")]
        public List<ToolParameterV1>? Parameters { get; set; }

        /// <summary>
        /// Parses a YAML string into an ExtendedToolV1 object.
        /// </summary>
        /// <param name="yaml">The YAML string to parse</param>
        /// <returns>The parsed ExtendedToolV1 object</returns>
        public static ExtendedToolV1 ParseYaml(string yaml)
        {
            var deserializer = GetDeserializerBuilder().Build();
            return deserializer.Deserialize<ExtendedToolV1>(yaml);
        }

        /// <summary>
        /// Loads a YAML file and parses it into an ExtendedToolV1 object asynchronously.
        /// </summary>
        /// <param name="fileName">The file path to read and parse</param>
        /// <returns>The parsed ExtendedToolV1 object, or null if the file doesn't exist or parsing fails</returns>
        public static async Task<ExtendedToolV1?> LoadYamlAsync(string fileName)
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
    /// Parameter specification for tool parameters.
    /// </summary>
    public class ToolParameterV1
    {
        [YamlMember(Alias = "name")]
        public string? Name { get; set; }

        [YamlMember(Alias = "type")]
        public string? Type { get; set; }

        [YamlMember(Alias = "description")]
        public string? Description { get; set; }

        [YamlMember(Alias = "map_to")]
        public string? MapTo { get; set; }

        [YamlMember(Alias = "required")]
        public bool? Required { get; set; }

        [YamlMember(Alias = "target")]
        public string? Target { get; set; }

        [YamlMember(Alias = "value")]
        public object? Value { get; set; }
    }
}
