// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Framework;

namespace Agent.Web.Models.ExtendedAgents.Response;

public class GeneratePythonToolResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("function_code")]
    public string FunctionCode { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public List<YamlParameter> Parameters { get; set; } = new();

    [JsonPropertyName("timeout_seconds")]
    public int TimeoutSeconds { get; set; } = 120;

    [JsonPropertyName("test_cases")]
    public List<TestCase> TestCases { get; set; } = new();

    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class TestCase
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public Dictionary<string, string> Parameters { get; set; } = new();

    [JsonPropertyName("expected_output")]
    public string? ExpectedOutput { get; set; }
}
