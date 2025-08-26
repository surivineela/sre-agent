// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Plugins.Implementation.AzureApplicationInsightsPlugin.Options;

namespace Agent.Plugins.Implementation.AzureApplicationInsightsPlugin.Models;

public class AppListTraceResult
{
    [JsonPropertyName("table")]
    public string Table { get; set; } = string.Empty;

    [JsonPropertyName("rows")]
    public List<AppListTraceEntry> Rows { get; set; } = new();
}
