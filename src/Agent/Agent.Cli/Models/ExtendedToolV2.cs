// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
    /// <summary>
    /// CLI YAML wrapper for tool configurations.
    /// Adds YAML envelope fields (api_version, kind) for file serialization.
    /// Supports polymorphic tool types (Kusto, Link, etc.) through the Spec property.
    /// The actual spec type is determined at runtime based on the 'type' field in the YAML.
    /// </summary>
    public class ExtendedToolV2 : ResourceModel
    {
        public ExtendedToolV2()
        {
            ApiVersion = YamlApiVersion.V2;
            Kind = ResourceKind.ExtendedAgentToolV2;
        }

        /// <summary>
        /// Resource metadata (owner, tags, version, timestamps).
        /// </summary>
        [YamlMember(Alias = "metadata", Order = 0)]
        public ResourceMetadataModel Metadata { get; set; } = new();

        /// <summary>
        /// Tool specification properties.
        /// This will be deserialized as the appropriate tool spec type (KustoToolSpecV2, LinkToolSpecV2, etc.)
        /// based on the 'type' field in the YAML.
        /// </summary>
        [YamlMember(Alias = "spec", Order = 1)]
        public ToolSpecV2 Spec { get; set; } = new();

        /// <summary>
        /// Normalizes string properties to ensure clean YAML literal block formatting.
        /// Removes trailing whitespace from description and query fields.
        /// </summary>
        public override void Normalize()
        {
            if (Spec != null)
            {
                Spec.Description = NormalizeString(Spec.Description);

                // Normalize Kusto-specific fields if this is a Kusto tool
                if (Spec is KustoToolSpecV2 kustoSpec)
                {
                    kustoSpec.Query = NormalizeString(kustoSpec.Query);
                }

                // Normalize Link-specific fields if this is a Link tool
                if (Spec is LinkToolSpecV2 linkSpec)
                {
                    linkSpec.Template = NormalizeString(linkSpec.Template);
                }
            }
        }

        /// <summary>
        /// Parses a YAML string into an ExtendedToolV2 object.
        /// Uses custom type converter to handle polymorphic tool specs based on spec.type field.
        /// </summary>
        /// <param name="yaml">The YAML string to parse</param>
        /// <returns>The parsed ExtendedToolV2 object</returns>
        public static ExtendedToolV2 ParseYaml(string yaml)
        {
            var deserializer = GetDeserializerBuilder()
                .WithTypeConverter(new ExtendedToolV2TypeConverter())
                .Build();
            return deserializer.Deserialize<ExtendedToolV2>(yaml);
        }

        /// <summary>
        /// Custom type converter that handles polymorphic ExtendedToolV2 deserialization.
        /// Determines the concrete ToolSpecV2 type based on the 'spec.type' field.
        /// </summary>
        private class ExtendedToolV2TypeConverter : IYamlTypeConverter
        {
            public bool Accepts(Type type)
            {
                return type == typeof(ExtendedToolV2);
            }

            public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
            {
                // First, deserialize as a generic dictionary to inspect the structure
                var deserializer = GetDeserializerBuilder().Build();

                var rawData = deserializer.Deserialize<Dictionary<string, object>>(parser);
                if (rawData == null)
                {
                    return null;
                }

                // Determine the tool spec type from spec.type field
                Type specType = typeof(ToolSpecV2);
                if (rawData.TryGetValue("spec", out var specObj) && specObj is Dictionary<object, object> specDict)
                {
                    if (specDict.TryGetValue("type", out var typeValue))
                    {
                        var toolType = typeValue?.ToString();
                        specType = toolType switch
                        {
                            var t when ToolName.KustoTool == t => typeof(KustoToolSpecV2),
                            var t when ToolName.LinkTool == t => typeof(LinkToolSpecV2),
                            _ => typeof(ToolSpecV2)
                        };
                    }
                }

                // Serialize back to YAML
                var serializer = new SerializerBuilder().Build();
                var yaml = serializer.Serialize(rawData);

                // Create a custom deserializer that knows the concrete spec type
                var finalDeserializer = GetDeserializerBuilder()
                    .WithTagMapping("!spec", specType)
                    .Build();

                // Deserialize the spec section with the correct type
                var tool = new ExtendedToolV2();

                if (rawData.TryGetValue("apiVersion", out var apiVer) && apiVer?.ToString() is string apiVerStr)
                {
                    var parsedVersion = YamlApiVersion.Parse(apiVerStr);
                    tool.ApiVersion = parsedVersion ?? YamlApiVersion.V2; // Default to V2 if parsing fails
                }

                if (rawData.TryGetValue("kind", out var kind) && kind?.ToString() is string kindStr)
                {
                    tool.Kind = kindStr;
                }

                if (rawData.TryGetValue("metadata", out var metadata) && metadata != null)
                {
                    var metaSerializer = new SerializerBuilder().Build();
                    var metaYaml = metaSerializer.Serialize(metadata);
                    tool.Metadata = finalDeserializer.Deserialize<ResourceMetadataModel>(metaYaml);
                }

                if (rawData.TryGetValue("spec", out var spec) && spec != null)
                {
                    var specSerializer = new SerializerBuilder().Build();
                    var specYaml = specSerializer.Serialize(spec);
                    var deserializedSpec = finalDeserializer.Deserialize(specYaml, specType);
                    if (deserializedSpec != null)
                    {
                        tool.Spec = (ToolSpecV2)deserializedSpec;
                    }
                }

                return tool;
            }

            public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
            {
                // Use default serialization
                serializer(value, type);
            }
        }

        /// <summary>
        /// Loads a YAML file and parses it into an ExtendedToolV2 object asynchronously.
        /// Uses custom type converter to handle polymorphic tool specs based on spec.type field.
        /// </summary>
        /// <param name="fileName">The file path to read and parse</param>
        /// <returns>The parsed ExtendedToolV2 object, or null if the file doesn't exist or parsing fails</returns>
        public static async Task<ExtendedToolV2?> LoadYamlAsync(string fileName)
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

        /// <summary>
        /// Validates the tool configuration.
        /// Returns a list of validation error messages. Empty list indicates no errors.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            // Validate metadata
            if (string.IsNullOrWhiteSpace(Metadata?.Name))
            {
                errors.Add("Tool must have a name in metadata.");
            }

            // Validate spec exists
            if (Spec == null)
            {
                errors.Add("Tool must have a spec section.");
                return errors; // Can't continue validation without spec
            }

            // Validate common spec fields
            if (string.IsNullOrWhiteSpace(Spec.Type))
            {
                errors.Add("Tool must have a 'type' specified.");
            }

            if (string.IsNullOrWhiteSpace(Spec.Description))
            {
                errors.Add("Tool must have a 'description'.");
            }

            // Type-specific validation
            if (!string.IsNullOrWhiteSpace(Spec.Type))
            {
                if (ToolName.KustoTool == Spec.Type)
                {
                    errors.AddRange(ValidateKustoTool());
                }
                else if (ToolName.LinkTool == Spec.Type)
                {
                    errors.AddRange(ValidateLinkTool());
                }
                else
                {
                    errors.Add($"Unsupported tool type: {Spec.Type}");
                }
            }

            // Validate parameters if present
            if (Spec.Parameters != null && Spec.Parameters.Count > 0)
            {
                errors.AddRange(ValidateParameters());
            }

            return errors;
        }

        private List<string> ValidateKustoTool()
        {
            var errors = new List<string>();

            if (Spec is not KustoToolSpecV2 kustoSpec)
            {
                errors.Add("KustoTool must use KustoToolSpecV2.");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(kustoSpec.Connector))
            {
                errors.Add("KustoTool must have a 'connector' specified.");
            }

            if (string.IsNullOrWhiteSpace(kustoSpec.Database))
            {
                errors.Add("KustoTool must have a 'database' specified.");
            }

            if (string.IsNullOrWhiteSpace(kustoSpec.Query))
            {
                errors.Add("KustoTool must have a 'query' specified.");
            }

            return errors;
        }

        private List<string> ValidateLinkTool()
        {
            var errors = new List<string>();

            if (Spec is not LinkToolSpecV2 linkSpec)
            {
                errors.Add("LinkTool must use LinkToolSpecV2.");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(linkSpec.Template))
            {
                errors.Add("LinkTool must have a 'template' specified.");
            }

            return errors;
        }

        private List<string> ValidateParameters()
        {
            var errors = new List<string>();

            if (Spec.Parameters == null) return errors;

            foreach (var param in Spec.Parameters)
            {
                if (string.IsNullOrWhiteSpace(param.Name))
                {
                    errors.Add("Parameter must have a 'name'.");
                    continue; // Skip further validation for this parameter
                }

                if (string.IsNullOrWhiteSpace(param.Type))
                {
                    errors.Add($"Parameter '{param.Name}' must have a 'type'.");
                }
                else
                {
                    // Validate parameter type
                    var validTypes = new[] { "string", "int", "bool", "float", "double", "object", "array" };
                    if (!validTypes.Contains(param.Type.ToLowerInvariant()))
                    {
                        errors.Add($"Parameter '{param.Name}' has invalid type '{param.Type}'. Must be one of: {string.Join(", ", validTypes)}");
                    }
                }

                if (string.IsNullOrWhiteSpace(param.Description))
                {
                    errors.Add($"Parameter '{param.Name}' must have a 'description'.");
                }

                // Validate mapTo if present
                if (!string.IsNullOrWhiteSpace(param.MapTo))
                {
                    var validMapTo = new[] { "args", "context", "body" };
                    if (!validMapTo.Contains(param.MapTo.ToLowerInvariant()))
                    {
                        errors.Add($"Parameter '{param.Name}' has invalid mapTo '{param.MapTo}'. Must be one of: {string.Join(", ", validMapTo)}");
                    }
                }
            }

            return errors;
        }
    }
}
