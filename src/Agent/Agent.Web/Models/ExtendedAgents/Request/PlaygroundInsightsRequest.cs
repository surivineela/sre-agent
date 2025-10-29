// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Agent.Web.Models.ExtendedAgents.Request;

public class PlaygroundInsightEvidence
{
    [JsonPropertyName("title")]
    [Required]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "info";
}

public class PlaygroundInsightsRequest
{
    [JsonPropertyName("prompt")]
    [Required]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("agentName")]
    public string? AgentName { get; set; }

    [JsonPropertyName("agentGoal")]
    public string? AgentGoal { get; set; }

    [JsonPropertyName("tools")]
    public List<string> Tools { get; set; } = new();

    [JsonPropertyName("systemTools")]
    public List<string> SystemTools { get; set; } = new();

    [JsonPropertyName("availableTools")]
    public List<string>? AvailableTools { get; set; }

    [JsonPropertyName("availableSystemTools")]
    public List<string>? AvailableSystemTools { get; set; }

    [JsonPropertyName("chatFindings")]
    public List<PlaygroundInsightEvidence> ChatFindings { get; set; } = new();

    [JsonPropertyName("toolFindings")]
    public List<PlaygroundInsightEvidence> ToolFindings { get; set; } = new();

    [JsonPropertyName("transcriptSummary")]
    public string? TranscriptSummary { get; set; }

    [JsonPropertyName("recentMessages")]
    public List<string> RecentMessages { get; set; } = new();
}
