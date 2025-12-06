// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
    /// <summary>
    /// Kusto-specific tool specification for YAML configurations (V1).
    /// Extends the base tool with Kusto-specific properties.
    /// </summary>
    public class KustoToolV1 : ExtendedToolV1
    {
        [YamlMember(Alias = "query", ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
        public string? Query { get; set; }

        [YamlMember(Alias = "database")]
        public string? Database { get; set; }

        /// <summary>
        /// Parses a YAML string into a KustoToolV1 object.
        /// </summary>
        /// <param name="yaml">The YAML string to parse</param>
        /// <returns>The parsed KustoToolV1 object</returns>
        public static new KustoToolV1 ParseYaml(string yaml)
        {
            var deserializer = KustoToolV1.GetDeserializerBuilder().Build();
            return deserializer.Deserialize<KustoToolV1>(yaml);
        }

        /// <summary>
        /// Loads a YAML file and parses it into a KustoToolV1 object asynchronously.
        /// </summary>
        /// <param name="fileName">The file path to read and parse</param>
        /// <returns>The parsed KustoToolV1 object, or null if the file doesn't exist or parsing fails</returns>
        public static new async Task<KustoToolV1?> LoadYamlAsync(string fileName)
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
