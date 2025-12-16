// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Cli.Models;

public class CliConfiguration
{
    [JsonPropertyName("resource_url")]
    public string ResourceUrl { get; set; } = string.Empty;

    [JsonPropertyName("auth_required")]
    public bool AuthRequired { get; set; }

    [JsonPropertyName("last_updated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
