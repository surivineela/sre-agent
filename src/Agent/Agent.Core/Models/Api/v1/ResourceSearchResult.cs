// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Core.Models.Api.v1;

public class ResourceSearchResult
{
    [JsonPropertyName("resource_id")]
    public string ResourceId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("subscription_id")]
    public string? SubscriptionId { get; set; }

    [JsonPropertyName("resource_group")]
    public string? ResourceGroup { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("properties")]
    public Dictionary<string, object>? Properties { get; set; }
}
