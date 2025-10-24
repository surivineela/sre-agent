// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Agent.Core.Models
{
    /// <summary>
    /// Contract for the POST body to bulk insights API.
    /// </summary>
    public class BulkInsightsPostBodyContract
    {
        [JsonPropertyName("apps")]
        public List<string> Apps { get; set; } = new();
    }
}
