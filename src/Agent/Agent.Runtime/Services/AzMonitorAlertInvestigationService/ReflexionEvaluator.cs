// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agent.Core.Services;
using Agent.Logging;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services.AzMonitorAlertInvestigation;

/// <summary>
/// Evaluates the quality of an investigation and provides guidance on next steps
/// </summary>
public class ReflexionEvaluator : IReflexionEvaluator
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<ReflexionEvaluator> _logger;

    private static readonly HashSet<string> AllSteps = new(StringComparer.OrdinalIgnoreCase)
    {
        "AnalyzeApplicationHealth",
        "AnalyzeActivityLogs",
        "AnalyzeConnectedComponents",
        "AnalyzeLogQueries",
        "AnalyzeResourceMetrics"
    };

    public ReflexionEvaluator(IChatClient chatClient, ILogger<ReflexionEvaluator> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<ReflexionResult> EvaluateInvestigationAsync(
        InvestigationContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string prompt = BuildReflexionPrompt(context);
            var options = new ChatOptions
            {
                Temperature = (float)0.1,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["response_format"] = "json"
                }
            };
            var response = await _chatClient.GetResponseAsync(
                new List<ChatMessage> { new ChatMessage(ChatRole.System, prompt) },
                options);
            return DeserializeReflexionResponse(response.Text, context);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error during reflexion evaluation");
            // Return a safe fallback
            return CreateFallbackReflexionResult(context);
        }
    }

    private string BuildReflexionPrompt(InvestigationContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an expert Azure SRE evaluating the quality and completeness of an alert investigation.");
        sb.AppendLine();
        // Add alert details
        sb.AppendLine("# Alert Details");
        sb.AppendLine("```");
        sb.AppendLine(FormatAlertDetails(context.Alert));
        sb.AppendLine("```");
        sb.AppendLine();
        // Add collected evidence
        sb.AppendLine("# Evidence Collected");
        foreach (var evidence in context.CollectedEvidence)
        {
            sb.AppendLine($"## {evidence.Key}");
            sb.AppendLine("```");
            sb.AppendLine(TruncateIfNeeded(evidence.Value.RawOutput, 1000));
            sb.AppendLine("```");
            sb.AppendLine();
        }
        // Add current hypotheses
        sb.AppendLine("# Current Hypotheses");
        if (context.CurrentHypotheses.Any())
        {
            foreach (var hypothesis in context.CurrentHypotheses)
            {
                sb.AppendLine($"- {hypothesis.Description} (Confidence: {hypothesis.Confidence:P0})");
                sb.AppendLine("  - Supporting Evidence:");
                foreach (var evidence in hypothesis.SupportingEvidence)
                {
                    sb.AppendLine($"    - {evidence}");
                }
                if (hypothesis.ConflictingEvidence.Any())
                {
                    sb.AppendLine("  - Conflicting Evidence:");
                    foreach (var evidence in hypothesis.ConflictingEvidence)
                    {
                        sb.AppendLine($"    - {evidence}");
                    }
                }
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("No hypotheses generated yet.");
            sb.AppendLine();
        }
        // Add steps completed and available
        sb.AppendLine("# Investigation Status");
        sb.AppendLine($"- Completed Steps: {string.Join(", ", context.CompletedSteps)}");
        sb.AppendLine($"- Available Steps: {string.Join(", ", GetUncompletedSteps(context))}");
        sb.AppendLine($"- Iteration Count: {context.IterationCount}");
        sb.AppendLine();
        // Add task description
        sb.AppendLine(@"# Your Task
Critically evaluate this investigation to determine:
1. Is there sufficient evidence to reach a conclusive root cause?
2. Are there specific knowledge gaps that need to be addressed?
3. Which additional investigation step would provide the most valuable information?
Return your evaluation as a JSON object with the following structure:
```json
{
  ""overallConfidence"": 0.75,
  ""continueInvestigation"": true,
  ""feedbackSuggestions"": [
    ""The application health metrics don't fully explain the latency spike"",
    ""No evidence has been collected about recent deployments""
  ],
  ""recommendedNextSteps"": [
    ""ActivityLogAnalysis"",
    ""LogQueryAnalysis""
  ]
}
```
Notes:
- overallConfidence should be between 0.0-1.0
- continueInvestigation should be false only if a high-confidence root cause is identified OR all steps have been exhausted
- feedbackSuggestions should contain specific observations about the investigation quality
- recommendedNextSteps must only include steps from this list: ApplicationHealth, ActivityLogAnalysis, ConnectedComponentsAnalysis, LogQueryAnalysis, MetricsAnalysis");
        return sb.ToString();
    }

    private ReflexionResult DeserializeReflexionResponse(string response, InvestigationContext context)
    {
        try
        {
            // Try to extract JSON from the response
            var match = Regex.Match(response, @"\{[\s\S]*\}");
            string jsonContent = match.Success ? match.Value : response;
            var reflexionResponse = JsonSerializer.Deserialize<ReflexionResponse>(jsonContent);
            if (reflexionResponse == null)
            {
                _logger.LogInternalWarning("Could not deserialize reflexion response");
                return CreateFallbackReflexionResult(context);
            }
            // Validate and filter the recommended steps
            var validNextSteps = ValidateNextSteps(reflexionResponse.RecommendedNextSteps, context);
            return new ReflexionResult
            {
                OverallConfidence = reflexionResponse.OverallConfidence,
                ContinueInvestigation =
                    reflexionResponse.ContinueInvestigation &&
                    validNextSteps.Any() &&
                    context.IterationCount < 5, // Hard limit on iterations
                FeedbackSuggestions = reflexionResponse.FeedbackSuggestions,
                RecommendedNextSteps = validNextSteps
            };
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error deserializing reflexion response");
            return CreateFallbackReflexionResult(context);
        }
    }

    private List<string> ValidateNextSteps(List<string> recommendedSteps, InvestigationContext context)
    {
        var uncompletedSteps = GetUncompletedSteps(context);
        // Filter to only include valid, uncompleted steps
        return recommendedSteps
            .Where(step =>
                AllSteps.Contains(step) &&
                !context.CompletedSteps.Contains(step, StringComparer.OrdinalIgnoreCase))
            .Take(2) // Limit to top 2 recommendations - change based on testing?
            .ToList();
    }

    private List<string> GetUncompletedSteps(InvestigationContext context)
    {
        return AllSteps
            .Where(step => !context.CompletedSteps.Contains(step, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private ReflexionResult CreateFallbackReflexionResult(InvestigationContext context)
    {
        var uncompletedSteps = GetUncompletedSteps(context);
        return new ReflexionResult
        {
            OverallConfidence = CalculateFallbackConfidence(context),
            ContinueInvestigation = uncompletedSteps.Any() && context.IterationCount < 5,
            FeedbackSuggestions = new List<string> { "Continue gathering evidence to form a more complete picture" },
            RecommendedNextSteps = uncompletedSteps.Take(2).ToList()
        };
    }

    private float CalculateFallbackConfidence(InvestigationContext context)
    {
        // fallback confidence heuristic? revisit this
        float stepsCompletionFactor = (float)context.CompletedSteps.Count / AllSteps.Count;
        float hypothesisConfidence = context.CurrentHypotheses
            .OrderByDescending(h => h.Confidence)
            .FirstOrDefault()?.Confidence ?? 0.0f;
        return Math.Max(stepsCompletionFactor, hypothesisConfidence);
    }

    private string FormatAlertDetails(AlertItem alert)
    {
        var essentials = alert.Properties?.Essentials;
        return $@"ID: {alert.Id ?? "Unknown"}
            Name: {alert.Name ?? "Unknown"}
            Rule: {essentials?.AlertRule ?? "Unknown"}
            Severity: {essentials?.Severity ?? "Unknown"}
            Condition: {essentials?.MonitorCondition ?? "Unknown"}
            Description: {essentials?.Description ?? "Unknown"}
            Resource: {essentials?.TargetResourceName ?? essentials?.TargetResource ?? "Unknown"}
            Type: {essentials?.TargetResourceType ?? "Unknown"}
            Time: {essentials?.StartDateTime ?? "Unknown"}";
    }

    private string TruncateIfNeeded(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text.Substring(0, maxLength) + "... [truncated]";
    }
}
