// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Cli.Models
{
    /// <summary>
    /// Generic envelope for V2 API resource responses.
    /// Used when deserializing resource data from V2 API endpoints.
    /// 
    /// The V2 API returns resources in this format:
    /// {
    ///   "name": "resource-name",
    ///   "type": "ResourceType", 
    ///   "properties": { /* resource-specific properties */ },
    ///   "tags": [...],
    ///   "owner": "..."
    /// }
    /// </summary>
    public class ApiEnvelope<T>
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("owner")]
        public string? Owner { get; set; }

        [JsonPropertyName("properties")]
        public T? Properties { get; set; }
    }
}
