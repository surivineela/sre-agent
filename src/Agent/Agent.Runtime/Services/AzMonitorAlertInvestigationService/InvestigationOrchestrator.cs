// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Logging;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Runtime.Services.AzMonitorAlertInvestigation;

/// <summary>
/// Orchestrates the entire investigation process
/// </summary>
public class InvestigationOrchestrator : IInvestigationOrchestrator
{
    private readonly IEnumerable<IReasoningStep> _reasoningSteps;
    private readonly IReflexionEvaluator _reflexionEvaluator;
    private readonly IHypothesisGenerator _hypothesisGenerator;
    private readonly IThreadRepository _repository;
    private readonly IChatClient _chatClient;
    private readonly ILogger<InvestigationOrchestrator> _logger;

    public InvestigationOrchestrator(
        IEnumerable<IReasoningStep> reasoningSteps,
        IReflexionEvaluator reflexionEvaluator,
        IHypothesisGenerator hypothesisGenerator,
        IThreadRepository repository,
        IChatClient chatClient,
        ILogger<InvestigationOrchestrator> logger)
    {
        _reasoningSteps = reasoningSteps;
        _reflexionEvaluator = reflexionEvaluator;
        _hypothesisGenerator = hypothesisGenerator;
        _repository = repository;
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<InvestigationSummary> InvestigateAlertAsync(
        AlertItem alert,
        Thread alertThread,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation($"Starting investigation loop for alert {alert.Id}");
        var agentContexts = await _repository.GetAgentContextsForThreadAsync(alertThread.Id);
        if (!agentContexts.Any())
        {
            _logger.LogInternalError("No agent context found for thread");
            return new InvestigationSummary
            {
                Summary = "Error: No agent context found for thread.",
                OverallConfidence = 0
            };
        }
        var context = new InvestigationContext(
            alertThread.Id,
            agentContexts.First().Id,
            alert);
        // Create an initial investigation progress message
        var progressMessageId = await InitProgressMessageAsync(alertThread.Id);
        bool continueInvestigation = true;
        int maxIterations = 2; // make it configurable ?

        // Add an initial tracking message to the thread
        await UpdateProgressMessageAsync(alertThread.Id, progressMessageId, context, "Starting investigation...");
        try
        {
            while (continueInvestigation &&
                  context.IterationCount < maxIterations &&
                  !cancellationToken.IsCancellationRequested)
            {
                // Select the next step to execute
                var nextStep = SelectNextStep(context);
                if (nextStep == null)
                {
                    _logger.LogInternalInformation("No more steps to execute");
                    break;
                }
                _logger.LogInternalInformation($"Executing step: {nextStep.StepName}");
                // Update the progress message
                await UpdateProgressMessageAsync(
                    alertThread.Id,
                    progressMessageId,
                    context,
                    $"Running investigation step: {GetFriendlyStepName(nextStep.StepName)}");
                // Execute the step
                var result = await nextStep.ExecuteAsync(alert, context, cancellationToken);
                context.CollectedEvidence[nextStep.StepName] = result;
                context.CompletedSteps.Add(nextStep.StepName);
                // Generate/update hypotheses based on the new evidence
                var hypotheses = await _hypothesisGenerator.GenerateHypothesesAsync(context, cancellationToken);
                if (hypotheses.Any())
                {
                    context.CurrentHypotheses = hypotheses;
                }
                // Update progress with new hypotheses
                await UpdateProgressMessageAsync(
                    alertThread.Id,
                    progressMessageId,
                    context,
                    $"Completed step: {GetFriendlyStepName(nextStep.StepName)}, generating hypotheses...");
                // Evaluate the investigation progress
                if (context.IterationCount > 0 || context.CompletedSteps.Count >= 2)
                {
                    var reflexionResult = await _reflexionEvaluator.EvaluateInvestigationAsync(context, cancellationToken);
                    context.LastReflexion = reflexionResult;
                    continueInvestigation = reflexionResult.ContinueInvestigation;
                    await UpdateProgressMessageAsync(
                        alertThread.Id,
                        progressMessageId,
                        context,
                        $"Evaluated investigation progress (confidence: {reflexionResult.OverallConfidence:P0})");
                    // Store reflexion feedback
                    await StoreReflexionFeedbackAsync(context);
                }
                context.IterationCount++;
                // Break if we've collected enough evidence and confidence is high
                if (context.LastReflexion?.OverallConfidence >= 0.8f && context.CompletedSteps.Count >= 3)
                {
                    _logger.LogInternalInformation("Investigation reached high confidence, stopping early");
                    break;
                }
            }
            // Generate the final summary
            var finalSummary = await GenerateFinalSummaryAsync(context, cancellationToken);
            // Update the progress message with completion
            await UpdateProgressMessageAsync(
                alertThread.Id,
                progressMessageId,
                context,
                title: "Investigation complete ✓",
                loading: "completed",
                isFinal: true);
            return finalSummary;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error during investigation loop");
            // Update the progress message with error
            await UpdateProgressMessageAsync(
                alertThread.Id,
                progressMessageId,
                context,
                $"Investigation error: {ex.Message}",
                 loading: "completed",
                isFinal: true);
            return new InvestigationSummary

            {
                Summary = $"Error during investigation: {ex.Message}",
                OverallConfidence = 0,
                InvestigationSteps = context.CompletedSteps,
                FinalHypotheses = context.CurrentHypotheses
            };
        }
    }

    private IReasoningStep SelectNextStep(InvestigationContext context)
    {
        // Select next step based on:
        // 1. Steps not yet executed
        // 2. Reflexion recommendations
        // 3. Current hypothesis confidence levels
        if (context.LastReflexion?.RecommendedNextSteps?.Any() == true)
        {
            foreach (var recommendedStep in context.LastReflexion.RecommendedNextSteps)
            {
                var step = _reasoningSteps.FirstOrDefault(s =>
                    s.StepName.Equals(recommendedStep, StringComparison.OrdinalIgnoreCase) &&
                    !context.CompletedSteps.Contains(s.StepName, StringComparer.OrdinalIgnoreCase));
                if (step != null) return step;
            }
        }
        // Otherwise, select based on default priority
        return _reasoningSteps
            .Where(s => !context.CompletedSteps.Contains(s.StepName, StringComparer.OrdinalIgnoreCase))
            .OrderBy(s => s.DefaultPriority)
            .FirstOrDefault();
    }

    private async Task<InvestigationSummary> GenerateFinalSummaryAsync(
        InvestigationContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            string summarizePrompt = BuildFinalSummaryPrompt(context);
            var options = new ChatOptions
            {
                Temperature = (float)0.1,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["response_format"] = "text"
                }
            };
            var response = await _chatClient.GetResponseAsync(
                new List<ChatMessage> { new ChatMessage(ChatRole.System, summarizePrompt) },
                options);
            string recommendedAction = DetermineRecommendedAction(context);
            return new InvestigationSummary
            {
                Summary = response.Text,
                FinalHypotheses = context.CurrentHypotheses,
                OverallConfidence = context.LastReflexion?.OverallConfidence ?? 0.5f,
                InvestigationSteps = context.CompletedSteps,
                RecommendedAction = recommendedAction
            };
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error generating final summary");
            return new InvestigationSummary
            {
                Summary = $"Error generating investigation summary: {ex.Message}",
                FinalHypotheses = context.CurrentHypotheses,
                OverallConfidence = context.LastReflexion?.OverallConfidence ?? 0.0f,
                InvestigationSteps = context.CompletedSteps
            };
        }
    }

    private string BuildFinalSummaryPrompt(InvestigationContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"TASK:
You are an AI assistant helping a Site Reliability Engineer analyze an Azure Monitor alert.
The following context contains the results of an automated investigation into an Azure Monitor alert.");
        // Add alert details
        sb.AppendLine();
        sb.AppendLine("### ALERT DETAILS");
        sb.AppendLine(FormatAlertDetailsForSummary(context.Alert));
        sb.AppendLine();
        // Add collected evidence organized by type
        foreach (var evidence in context.CollectedEvidence)
        {
            sb.AppendLine($"### {GetFriendlyStepName(evidence.Key)}");
            sb.AppendLine(TruncateIfNeeded(evidence.Value.RawOutput, 2000));
            sb.AppendLine();
        }
        // Add instructions for final summary
        sb.AppendLine(@"---
Based on the initial investigation summary, analyze the evidence and provide:
## Summary of Findings
- [Specific finding with exact metric/timestamp/error] 
- [Specific finding with exact metric/timestamp/error]
## Hypotheses
### Hypothesis 1 (Confidence: XX%)
One sentence describing specific cause with exact evidence values supporting it
### Hypothesis 2 (Confidence: XX%) [Optional]
One sentence describing specific cause with exact evidence values supporting it
Remember: Quality findings with specific values are better than quantity. Exclude any hypothesis without concrete supporting evidence.");
        return sb.ToString();
    }

    private string DetermineRecommendedAction(InvestigationContext context)
    {
        // If high confidence in a hypothesis, recommend action based on it
        var bestHypothesis = context.CurrentHypotheses
            .OrderByDescending(h => h.Confidence)
            .FirstOrDefault();
        if (bestHypothesis != null && bestHypothesis.Confidence > 0.7f)
        {
            return $"Consider addressing the probable root cause: {bestHypothesis.Description}";
        }
        return "Continue monitoring and collect more diagnostic data to improve confidence in the analysis.";
    }

    private async Task<Guid> InitProgressMessageAsync(Guid threadId)
    {
        var messageId = Guid.NewGuid();

        Message initMessage = new Message(
        Id: messageId,
        TimeStamp: DateTime.UtcNow,
        Author: new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
        Text: ChatMessageService.InitializeInvestigationSummariesMessage(
            "Starting investigation and diagnosis",
            [
                new("Planning", "📝 Gathering information about the issue", true)
            ])
        );
        
        await _repository.AddMessageAsync(threadId, initMessage);
        return messageId;
    }

    private async Task UpdateProgressMessageAsync(
        Guid threadId,
        Guid messageId,
        InvestigationContext context,
        string title,
        string loading = "",
        bool isFinal = false)
    {
        try
        {
            var existingMessage = await _repository.GetMessageAsync(threadId, messageId);
            if (existingMessage == null) return;

            // Build summaries from completed steps and hypotheses
            var summaries = new List<(string, string, bool)>();

            // Add status as first item
            summaries.Add((title, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"), false));

            // Add step results
            foreach (var evidence in context.CollectedEvidence)
            {
                string evidenceTitle = $"Step: {GetFriendlyStepName(evidence.Key)}";
                string content = evidence.Value.Success
                    ? TruncateIfNeeded(evidence.Value.RawOutput, 300)
                    : $"Error: {evidence.Value.RawOutput}";
                summaries.Add((evidenceTitle, content, true)); // Collapsed by default
            }

            // Add current hypotheses if any
            if (context.CurrentHypotheses.Any())
            {
                string hypothesesSummary = string.Join("\n\n", context.CurrentHypotheses
                    .Select(h => $"Hypothesis ({h.Confidence:P0}): {h.Description}"));
                summaries.Add(("Current Hypotheses", hypothesesSummary, false));
            }

            // Add reflexion feedback if any
            // TODO: Probably shouldn't be exposed. But will use for debugging.
            if (context.LastReflexion != null && context.LastReflexion.FeedbackSuggestions.Any())
            {
                string feedback = string.Join("\n", context.LastReflexion.FeedbackSuggestions
                    .Select(f => $"- {f}"));
                summaries.Add(("Reflection Feedback", feedback, true));
            }

            if (summaries == null || summaries.Count == 0)
                return;
            //// Update message text 
            string updatedText = ChatMessageService.AppendInvestigationSummary(
                existingMessage.Text, title, FormatProgressMessage(summaries), status: loading, isFinal: isFinal);
            Message updatedMessage = existingMessage with { Text = updatedText }; // Todo: enable this after implementing UpdateInvestigationSummariesMessage

            await _repository.UpdateMessageAsync(threadId, updatedMessage);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error updating progress message");
        }
    }

    private string FormatProgressMessage(List<(string, string, bool)> messages)
    {
        StringBuilder sb = new();

        foreach (var message in messages)
        {
            sb.AppendLine(message.Item1).AppendLine(message.Item2);
        }

        return sb.ToString();
    }

    private async Task StoreReflexionFeedbackAsync(InvestigationContext context)
    {
        if (context.LastReflexion == null || !context.LastReflexion.FeedbackSuggestions.Any())
            return;
        try
        {
            await _repository.CreateReasoningMessageAsync(new ReasoningMessage(
                Guid.NewGuid(),
                context.AgentContextId,
                ReasoningMessageRoleEnum.System,
                JsonSerializer.Serialize(new
                {
                    description = "Reflexive evaluation of investigation quality",
                    confidence = context.LastReflexion.OverallConfidence,
                    feedback = context.LastReflexion.FeedbackSuggestions,
                    recommendedSteps = context.LastReflexion.RecommendedNextSteps
                })
            ));
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error storing reflexion feedback");
        }
    }

    private string GetFriendlyStepName(string stepName)
    {
        return stepName switch
        {
            "ApplicationHealth" => "Application Health Analysis",
            "ActivityLogAnalysis" => "Activity Log Analysis",
            "ConnectedComponentsAnalysis" => "Connected Components Analysis",
            "LogQueryAnalysis" => "Log Query Analysis",
            "MetricsAnalysis" => "Metrics Analysis",
            _ => stepName
        };
    }

    private string FormatAlertDetailsForSummary(AlertItem alert)
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
