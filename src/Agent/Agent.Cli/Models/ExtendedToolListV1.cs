// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
    /// <summary>
    /// Represents a list of tools in V1 format from a YAML file.
    /// The YAML structure contains metadata and a spec with an array of tool definitions.
    /// </summary>
    public class ExtendedToolListV1 : ResourceModel
    {
        [YamlMember(Alias = "metadata")]
        public ResourceMetadataModel? Metadata { get; set; }

        [YamlMember(Alias = "spec")]
        public ExtendedToolListSpecV1 Spec { get; set; } = new();

        /// <summary>
        /// Parses a YAML string into an ExtendedToolListV1 object.
        /// </summary>
        /// <param name="yaml">The YAML string to parse</param>
        /// <returns>The parsed ExtendedToolListV1 object</returns>
        public static ExtendedToolListV1 ParseYaml(string yaml)
        {
            var deserializer = GetDeserializerBuilder().Build();
            return deserializer.Deserialize<ExtendedToolListV1>(yaml);
        }

        /// <summary>
        /// Loads a YAML file and parses it into an ExtendedToolListV1 object asynchronously.
        /// </summary>
        /// <param name="fileName">The file path to read and parse</param>
        /// <returns>The parsed ExtendedToolListV1 object, or null if the file doesn't exist or parsing fails</returns>
        public static async Task<ExtendedToolListV1?> LoadYamlAsync(string fileName)
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
    /// Specification for a tool list, containing an array of tool definitions.
    /// </summary>
    public class ExtendedToolListSpecV1
    {
        [YamlMember(Alias = "tools")]
        public List<ExtendedToolItemV1> Tools { get; set; } = new();
    }

    /// <summary>
    /// Represents a single tool item in the tool list.
    /// This can be either a KustoTool or LinkTool, determined by the 'type' field.
    /// All tool properties are flattened into this class for simplicity.
    /// </summary>
    public class ExtendedToolItemV1
    {
        [YamlMember(Alias = "name")]
        public string? Name { get; set; }

        [YamlMember(Alias = "type")]
        public string? Type { get; set; }

        [YamlMember(Alias = "connector")]
        public string? Connector { get; set; }

        [YamlMember(Alias = "description", ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
        public string? Description { get; set; }

        [YamlMember(Alias = "parameters")]
        public List<ToolParameterV1>? Parameters { get; set; }

        [YamlMember(Alias = "attributes")]
        public List<string>? Attributes { get; set; }

        [YamlMember(Alias = "metadata")]
        public ResourceMetadataModel? Metadata { get; set; }

        // KustoTool-specific properties
        [YamlMember(Alias = "query", ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
        public string? Query { get; set; }

        [YamlMember(Alias = "database")]
        public string? Database { get; set; }

        // LinkTool-specific properties
        [YamlMember(Alias = "template")]
        public string? Template { get; set; }
    }
}
