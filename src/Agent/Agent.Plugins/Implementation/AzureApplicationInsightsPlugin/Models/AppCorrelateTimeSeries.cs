// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Plugins.Implementation.AzureApplicationInsightsPlugin.Models;

public class AppCorrelateTimeSeries
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("timeSeries")]
    [JsonConverter(typeof(RoundedDoubleArrayConverter))]
    public double[] Data { get; set; } = Array.Empty<double>();
}
