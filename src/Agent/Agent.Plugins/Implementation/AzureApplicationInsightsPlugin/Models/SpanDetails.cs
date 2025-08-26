// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Plugins.Implementation.AzureApplicationInsightsPlugin.Models;

public class SpanDetails
{
    [JsonPropertyName("itemId")]
    public string? ItemId { get; set; }

    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<KeyValuePair<string, string>> Properties { get; set; } = new List<KeyValuePair<string, string>>();
}
