// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Cli.Models
{
    /// <summary>
    /// Generic collection response for V2 API endpoints that return { "value": [...] }
    /// </summary>
    public class ApiCollectionEnvelope<T>
    {
        [JsonPropertyName("value")] public List<ApiEnvelope<T>>? Value { get; set; }
    }
}
