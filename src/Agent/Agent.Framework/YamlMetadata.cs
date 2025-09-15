// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace Agent.Framework.Reasoning.Models
{
    public class YamlMetadata
    {
        [YamlMember(Alias = "owner")]
        [JsonPropertyName("owner")]
        public string? Owner { get; set; }

        [YamlMember(Alias = "version")]
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [YamlMember(Alias = "tags")]
        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [YamlMember(Alias = "updated_at")]
        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [YamlMember(Alias = "created_at")]
        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }
    }

}
