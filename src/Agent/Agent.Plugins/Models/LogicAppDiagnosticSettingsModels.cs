// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Plugins.Models;

public sealed class DiagnosticSettingsEnvelope
{
    [JsonPropertyName("value")]
    public List<DiagnosticSetting>? Value { get; set; }
}

public sealed class DiagnosticSetting
{
    [JsonPropertyName("properties")]
    public DiagnosticSettingProperties? Properties { get; set; }
}

public sealed class DiagnosticSettingProperties
{
    [JsonPropertyName("metrics")]
    public List<MetricSetting>? Metrics { get; set; }

    [JsonPropertyName("logs")]
    public List<LogSetting>? Logs { get; set; }
}

public sealed class MetricSetting
{
    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
}

public sealed class LogSetting
{
    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
}
