// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Core.Services;
namespace Agent.Runtime.Models;

/// <summary>
/// Contains the complete context of an ongoing investigation
/// </summary>
public class InvestigationContext
{
    public Guid ThreadId { get; set; }
    public Guid AgentContextId { get; set; }
    public AlertItem Alert { get; set; }
    public List<Hypothesis> CurrentHypotheses { get; set; }
    public Dictionary<string, StepResult> CollectedEvidence { get; set; } = new();
    public int IterationCount { get; set; } = 0;
    public List<string> CompletedSteps { get; set; } = new();
    public ReflexionResult LastReflexion { get; set; }
    public InvestigationContext(Guid threadId, Guid agentContextId, AlertItem alert)
    {
        ThreadId = threadId;
        AgentContextId = agentContextId;
        Alert = alert;
        CollectedEvidence = new Dictionary<string, StepResult>();
        CompletedSteps = new List<string>();
        CurrentHypotheses = new List<Hypothesis>();
    }
}

/// <summary>
/// Represents a potential explanation for the alert condition
/// </summary>
public class Hypothesis
{
    public string Description { get; set; }
    public float Confidence { get; set; }
    public List<string> SupportingEvidence { get; set; }
    public List<string> ConflictingEvidence { get; set; }
}

/// <summary>
/// Result of executing a reasoning step
/// </summary>
public class StepResult
{
    public string StepName { get; set; }
    public string RawOutput { get; set; }
    public Dictionary<string, object> ExtractedData { get; set; } = new();
    public bool Success { get; set; }
    public DateTime Timestamp { get; set; }
    // For serialization
    public StepResult() { }
    public StepResult(string stepName, string rawOutput, bool success)
    {
        StepName = stepName;
        RawOutput = rawOutput;
        Success = success;
        Timestamp = DateTime.UtcNow;
        ExtractedData = new Dictionary<string, object>();
    }
}

/// <summary>
/// Result of the reflexion evaluation
/// </summary>
public class ReflexionResult
{
    public bool ContinueInvestigation { get; set; }
    public float OverallConfidence { get; set; }
    public List<string> FeedbackSuggestions { get; set; } = new();
    public List<string> RecommendedNextSteps { get; set; } = new();
}

/// <summary>
/// Final investigation summary to be returned to the caller
/// </summary>
public class InvestigationSummary
{
    public string Summary { get; set; }
    public List<Hypothesis> FinalHypotheses { get; set; } = new();
    public float OverallConfidence { get; set; }
    public List<string> InvestigationSteps { get; set; } = new();
    public string RecommendedAction { get; set; }
}

#region Helper Models for API Responses
/// <summary>
/// Helper class for deserializing reflexion evaluator responses
/// </summary>
public class ReflexionResponse
{
    [JsonPropertyName("overallConfidence")]
    public float OverallConfidence { get; set; }
    [JsonPropertyName("continueInvestigation")]
    public bool ContinueInvestigation { get; set; }
    [JsonPropertyName("feedbackSuggestions")]
    public List<string> FeedbackSuggestions { get; set; } = new();
    [JsonPropertyName("recommendedNextSteps")]
    public List<string> RecommendedNextSteps { get; set; } = new();
}

/// <summary>
/// Helper class for deserializing hypothesis generation responses
/// </summary>
public class HypothesisResponse
{
    [JsonPropertyName("hypotheses")]
    public List<HypothesisItem> Hypotheses { get; set; } = new();
    public class HypothesisItem
    {
        [JsonPropertyName("description")]
        public string Description { get; set; }
        [JsonPropertyName("confidence")]
        public float Confidence { get; set; }
        [JsonPropertyName("supportingEvidence")]
        public List<string> SupportingEvidence { get; set; } = new();
        [JsonPropertyName("conflictingEvidence")]
        public List<string> ConflictingEvidence { get; set; } = new();
    }
}
#endregion

