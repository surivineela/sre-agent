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

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("api_version")]
    public string ApiVersion { get; set; } = "azuresre.ai/v1";

    [JsonPropertyName("owner")]
    public string Owner { get; set; } = "your-team@example.com";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new List<string> { "example", "demo", "generic" };
}
