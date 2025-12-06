// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
    /// <summary>
    /// Link-specific tool specification for YAML configurations (V1).
    /// Extends the base tool with Link-specific properties.
    /// </summary>
    public class LinkToolV1 : ExtendedToolV1
    {
        [YamlMember(Alias = "template")]
        public string? Template { get; set; }

        /// <summary>
        /// Parses a YAML string into a LinkToolV1 object.
        /// </summary>
        /// <param name="yaml">The YAML string to parse</param>
        /// <returns>The parsed LinkToolV1 object</returns>
        public static new LinkToolV1 ParseYaml(string yaml)
        {
            var deserializer = LinkToolV1.GetDeserializerBuilder().Build();
            return deserializer.Deserialize<LinkToolV1>(yaml);
        }

        /// <summary>
        /// Loads a YAML file and parses it into a LinkToolV1 object asynchronously.
        /// </summary>
        /// <param name="fileName">The file path to read and parse</param>
        /// <returns>The parsed LinkToolV1 object, or null if the file doesn't exist or parsing fails</returns>
        public static new async Task<LinkToolV1?> LoadYamlAsync(string fileName)
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
