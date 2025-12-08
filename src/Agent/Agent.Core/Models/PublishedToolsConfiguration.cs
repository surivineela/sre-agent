// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Core.Models
{
    public class PublishedToolsConfiguration
    {
        [JsonPropertyName("tools")]
        public List<PublishedTool> Tools { get; set; } = new List<PublishedTool>();
    }

    public class PublishedTool
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("versions")]
        public Dictionary<string, string>? Versions { get; set; }

        [JsonPropertyName("firstPartyOnly")]
        public bool FirstPartyOnly { get; set; }

        /// <summary>
        /// User-friendly description for UI display. When set, this overrides the default tool description.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// Gets the effective tool name to use.
        /// If versions are defined, returns the latest version; otherwise returns the name itself.
        /// </summary>
        public string GetEffectiveToolName()
        {
            if (Versions != null && Versions.ContainsKey("latest"))
            {
                var latestVersion = Versions["latest"];
                return Versions.ContainsKey(latestVersion) ? Versions[latestVersion] : Name;
            }
            return Name;
        }
    }
}
