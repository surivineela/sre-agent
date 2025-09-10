// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Plugins.Models;

/// <summary>
/// Represents the response from Azure ARM API when retrieving function keys
/// </summary>
internal class FunctionKeyResponse
{
    [JsonPropertyName("responses")]
    public List<FunctionKeyBatchResponse>? Responses { get; set; }
}

internal class FunctionKeyBatchResponse
{
    [JsonPropertyName("httpStatusCode")]
    public int HttpStatusCode { get; set; }

    [JsonPropertyName("content")]
    public FunctionKeyContent? Content { get; set; }
}

internal class FunctionKeyContent
{
    [JsonPropertyName("masterKey")]
    public string? MasterKey { get; set; }

    [JsonPropertyName("functionKeys")]
    public Dictionary<string, string>? FunctionKeys { get; set; }

    [JsonPropertyName("systemKeys")]
    public Dictionary<string, string>? SystemKeys { get; set; }
}