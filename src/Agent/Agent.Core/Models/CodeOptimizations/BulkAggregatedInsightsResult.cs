// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Agent.Core.Models
{
    /// <summary>
    /// Represents a single result from the bulk code optimizations insights API.
    /// </summary>
    public class BulkAggregatedInsightsResult
    {
        [JsonPropertyName("appId")]
        public string AppId { get; set; } = string.Empty;

        [JsonPropertyName("roleName")]
        public string RoleName { get; set; } = string.Empty;

        [JsonPropertyName("insights")]
        public List<AggregatedInsightsContract> Insights { get; set; } = new();
    }
}
