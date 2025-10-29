// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Web.Models.ExtendedAgents.Response;

public class PlaygroundInsightImpact
{
    [JsonPropertyName("scoreIncrease")]
    public int ScoreIncrease { get; set; }

    [JsonPropertyName("dimension")]
    public string Dimension { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public string Level { get; set; } = string.Empty;
}

public class PlaygroundInsightAction
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "info";

    [JsonPropertyName("impact")]
    public PlaygroundInsightImpact? Impact { get; set; }

    [JsonPropertyName("patch")]
    public string? Patch { get; set; }

    [JsonPropertyName("autoApplicable")]
    public bool AutoApplicable { get; set; }

    [JsonPropertyName("conflicts")]
    public List<string>? Conflicts { get; set; }

    [JsonPropertyName("requires")]
    public List<string>? Requires { get; set; }
}

public class PlaygroundSubScore
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("evidence")]
    public string Evidence { get; set; } = string.Empty;

    [JsonPropertyName("improvements")]
    public List<string>? Improvements { get; set; }
}

public class PlaygroundInsightsResponse
{
    [JsonPropertyName("confidenceScore")]
    public double ConfidenceScore { get; set; }

    [JsonPropertyName("confidenceLabel")]
    public string ConfidenceLabel { get; set; } = string.Empty;

    [JsonPropertyName("subScores")]
    public List<PlaygroundSubScore> SubScores { get; set; } = new();

    [JsonPropertyName("promptInsights")]
    public List<string> PromptInsights { get; set; } = new();

    [JsonPropertyName("toolSuggestions")]
    public List<string> ToolSuggestions { get; set; } = new();

    [JsonPropertyName("chatDiagnostics")]
    public List<string> ChatDiagnostics { get; set; } = new();

    [JsonPropertyName("actionItems")]
    public List<PlaygroundInsightAction> ActionItems { get; set; } = new();

    [JsonPropertyName("notes")]
    public List<string> Notes { get; set; } = new();

    [JsonPropertyName("suggestedSequence")]
    public List<string>? SuggestedSequence { get; set; }
}
