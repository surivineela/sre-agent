// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Logging;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using OpenTelemetry;
using Thread = Agent.Core.Models.Api.v1.Thread;
using Agent.Data.DataModels.IncidentModel;
using Agent.Framework;

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
    private readonly IChatClientProvider _chatClientProvider;
    private readonly ILogger<InvestigationOrchestrator> _logger;
    private readonly Tracer _tracer;

    public InvestigationOrchestrator(
        IEnumerable<IReasoningStep> reasoningSteps,
        IReflexionEvaluator reflexionEvaluator,
        IHypothesisGenerator hypothesisGenerator,
        IThreadRepository repository,
        IChatClientProvider chatClientProvider,
        ILogger<InvestigationOrchestrator> logger,
        Tracer tracer)
    {
        _reasoningSteps = reasoningSteps;
        _reflexionEvaluator = reflexionEvaluator;
        _hypothesisGenerator = hypothesisGenerator;
        _repository = repository;
        _chatClientProvider = chatClientProvider;
        _logger = logger;
        _tracer = tracer;
    }

    public async Task<InvestigationSummary> InvestigateAlertAsync(
        AlertItem alert,
        Thread alertThread,
        CancellationToken cancellationToken = default)
    {
        // Create a root span for this investigation - each investigation gets its own isolated trace
        // The span will be properly processed by AgentTraceProcessor due to the "operation.name" attribute
        using var span = _tracer.StartSpan("investigate.alert", SpanKind.Internal);
        span.SetAttribute("operation.name", "investigate.alert");
        span.SetAttribute("alert.id", alert.Id ?? "unknown");
        span.SetAttribute("alert.name", alert.Name ?? "unknown");
        span.SetAttribute("alert.type", alert.Type ?? "unknown");
        span.SetAttribute("alert.properties", alert.Properties != null ? JsonSerializer.Serialize(alert.Properties) : "unknown");
        span.SetAttribute("thread.id", alertThread.Id.ToString());

        _logger.LogInternalInformation($"Starting investigation loop for alert {alert.Id}");

        var agentContexts = await _repository.GetAgentContextsForThreadAsync(alertThread.Id);

        if (!agentContexts.Any())
        {
            _logger.LogInternalError("No agent context found for thread");
            span.SetStatus(OpenTelemetry.Trace.Status.Error.WithDescription("No agent context found for thread"));

            return new InvestigationSummary
            {
                Summary = "Error: No agent context found for thread.",
                OverallConfidence = 0,
                RecommendedAction = "Please ensure the agent context is properly initialized.",
            };
        }

        var context = new InvestigationContext(
            alertThread.Id,
            agentContexts.First().Id,
            alert);

        // Store the root span in the investigation context for tracing
        context.RootSpan = span;

        // Create an initial investigation progress message
        var progressMessageId = await InitProgressMessageAsync(alertThread.Id);

        bool continueInvestigation = true;

        int maxIterations = 5; // right now this controls how many investigation steps are executed

        try
        {
            // Count total available reasoning steps to ensure we execute all at least once
            var totalReasoningSteps = _reasoningSteps.Count();

            while (continueInvestigation &&
                  context.IterationCount < maxIterations &&
                  !cancellationToken.IsCancellationRequested)
            {
                // Select the next step to execute
                var nextStep = SelectNextStep(context);

                if (nextStep == null)
                {
                    _logger.LogInternalInformation("All available steps have been executed");
                    break;
                }

                _logger.LogInternalInformation($"Executing step: {nextStep.StepName}");

                // Execute the step with tracing
                using var stepSpan = _tracer.StartActiveSpan("reasoning.step", SpanKind.Internal, span);
                stepSpan.SetAttribute("operation.name", "reasoning.step");
                stepSpan.SetAttribute("thread.id", alertThread.Id.ToString());
                stepSpan.SetAttribute("step.name", nextStep.StepName);

                var result = await nextStep.ExecuteAsync(alert, context, cancellationToken);
                context.CollectedEvidence[nextStep.StepName] = result;
                context.CompletedSteps.Add(nextStep.StepName);
                stepSpan.SetAttribute("step.success", true);
                stepSpan.SetAttribute("step.output", result.RawOutput ?? "No output");

                _logger.LogInternalInformation($"Step {nextStep.StepName} completed successfully");

                // Update progress with step-specific results (NOT hypotheses)
                await UpdateProgressMessageAsync(
                    alertThread.Id,
                    progressMessageId,
                    $"Finished {GetFriendlyStepName(nextStep.StepName)}",
                    result.RawOutput ?? "No output available");

                // Evaluate the investigation progress (but only after we've executed at least 2 steps)
                if (context.IterationCount > 0 || context.CompletedSteps.Count >= 2)
                {
                    using var reflexionSpan = _tracer.StartActiveSpan("evaluate.investigation", SpanKind.Internal, span);
                    reflexionSpan.SetAttribute("operation.name", "evaluate.investigation");
                    reflexionSpan.SetAttribute("thread.id", alertThread.Id.ToString());
                    var reflexionResult = await _reflexionEvaluator.EvaluateInvestigationAsync(context, cancellationToken);

                    context.LastReflexion = reflexionResult;
                    reflexionSpan.SetAttribute("reflexion.confidence", reflexionResult.OverallConfidence);
                    reflexionSpan.SetAttribute("reflexion.continue_investigation", reflexionResult.ContinueInvestigation);
                    reflexionSpan.SetAttribute("reflexion.feedback_suggestions", string.Join("; ", reflexionResult.FeedbackSuggestions ?? new List<string>()));
                    reflexionSpan.SetAttribute("reflexion.recommended_next_steps", string.Join("; ", reflexionResult.RecommendedNextSteps ?? new List<string>()));


                    // Only allow reflexion to stop investigation if all reasoning steps have been executed at least once
                    if (context.CompletedSteps.Count >= totalReasoningSteps)
                    {
                        continueInvestigation = reflexionResult.ContinueInvestigation;
                    }

                    // TODO: Add current confidence score.

                    // Store reflexion feedback
                    await StoreReflexionFeedbackAsync(context);
                }

                context.IterationCount++;

                // Only allow early termination if all reasoning steps have been executed at least once
                if (context.CompletedSteps.Count >= totalReasoningSteps &&
                    context.LastReflexion?.OverallConfidence >= 0.8f)
                {
                    _logger.LogInternalInformation("All steps executed and investigation reached high confidence, stopping early");
                    break;
                }
            }

            // Generate the final summary (this will also generate hypotheses)
            _logger.LogInternalInformation("All investigation steps completed. Generating final summary and hypotheses...");
            var finalSummary = await GenerateFinalSummaryAsync(context, cancellationToken);

            // Track final investigation metrics
            span.SetStatus(OpenTelemetry.Trace.Status.Ok);

            // Update the progress message with completion (this will show final hypotheses)
            await UpdateProgressMessageAsync(
                alertThread.Id,
                progressMessageId,
                title: "Investigation complete ✓",
                finalSummary.Summary,
                status: "completed",
                isFinal: true);
            return finalSummary;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error during investigation loop");
            span.SetStatus(OpenTelemetry.Trace.Status.Error.WithDescription($"Error during investigation: {ex.Message}"));
            span.SetAttribute("error.type", ex.GetType().Name);
            span.SetAttribute("error.message", ex.Message);

            return new InvestigationSummary

            {
                Summary = $"Error during investigation: {ex.Message}",
                OverallConfidence = 0,
                InvestigationSteps = context.CompletedSteps,
                FinalHypotheses = context.CurrentHypotheses,
                RecommendedAction = "Please check the logs for more details."
            };
        }
    }

    private IReasoningStep? SelectNextStep(InvestigationContext context)
    {
        // Priority 1: Execute all reasoning steps at least once
        // Get steps that haven't been executed yet, ordered by default priority
        var unexecutedSteps = _reasoningSteps
            .Where(s => !context.CompletedSteps.Contains(s.StepName, StringComparer.OrdinalIgnoreCase))
            .OrderBy(s => s.DefaultPriority)
            .ToList();

        // If there are unexecuted steps, select the next one by priority
        if (unexecutedSteps.Any())
        {
            return unexecutedSteps.First();
        }

        // Priority 2: If all steps have been executed at least once,
        // consider reflexion recommendations for re-execution
        if (context.LastReflexion?.RecommendedNextSteps?.Any() == true)
        {
            foreach (var recommendedStep in context.LastReflexion.RecommendedNextSteps)
            {
                var step = _reasoningSteps.FirstOrDefault(s =>
                    s.StepName.Equals(recommendedStep, StringComparison.OrdinalIgnoreCase));
                if (step != null) return step;
            }
        }

        // No more steps to execute
        return null;
    }

    private async Task<InvestigationSummary> GenerateFinalSummaryAsync(
        InvestigationContext context,
        CancellationToken cancellationToken)
    {
        using var span = _tracer.StartActiveSpan("generate.summary", SpanKind.Internal, context.RootSpan);
        span.SetAttribute("operation.name", "generate.summary");
        span.SetAttribute("thread.id", context.ThreadId.ToString());

        try
        {
            // generate hypotheses based on all collected evidence
            _logger.LogInternalInformation("Generating final hypotheses based on all collected evidence");

            using var hypothesesSpan = _tracer.StartActiveSpan("generate.hypotheses", SpanKind.Internal, context.RootSpan);
            hypothesesSpan.SetAttribute("operation.name", "generate.hypotheses");
            hypothesesSpan.SetAttribute("thread.id", context.ThreadId.ToString());
            var hypotheses = await _hypothesisGenerator.GenerateHypothesesAsync(context, cancellationToken);
            hypothesesSpan.SetAttribute("hypotheses.count", hypotheses.Count);
            hypothesesSpan.SetAttribute("hypotheses.descriptions", string.Join("; ", hypotheses.Select(h => $"{h.Description} (confidence: {h.Confidence:F2})")));


            if (hypotheses.Any())
            {
                context.CurrentHypotheses = hypotheses;
            }

            string summarizePrompt = BuildFinalSummaryPrompt(context);

            var options = new ChatOptions
            {
                Temperature = (float)0.1,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["response_format"] = "text"
                }
            };

            var response = await _chatClientProvider.GeneralPurposeModel.GetResponseAsync(
                new List<ChatMessage> { new ChatMessage(ChatRole.System, summarizePrompt) },
                options);

            string recommendedAction = DetermineRecommendedAction(context);

            span.SetAttribute("summary.content", response.Text);
            span.SetStatus(OpenTelemetry.Trace.Status.Ok);

            return new InvestigationSummary
            {
                Summary = response.Text ?? "No summary generated",
                FinalHypotheses = context.CurrentHypotheses,
                OverallConfidence = context.LastReflexion?.OverallConfidence ?? 0.5f,
                InvestigationSteps = context.CompletedSteps,
                RecommendedAction = recommendedAction
            };
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error generating final summary");
            span.SetStatus(OpenTelemetry.Trace.Status.Error.WithDescription($"Error generating final summary: {ex.Message}"));
            span.SetAttribute("error.type", ex.GetType().Name);
            span.SetAttribute("error.message", ex.Message);

            return new InvestigationSummary
            {
                Summary = $"Error generating investigation summary: {ex.Message}",
                FinalHypotheses = context.CurrentHypotheses,
                OverallConfidence = context.LastReflexion?.OverallConfidence ?? 0.0f,
                InvestigationSteps = context.CompletedSteps,
                RecommendedAction = "Please check the logs for more details."
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
            sb.AppendLine(evidence.Value.RawOutput);
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
CRITICAL: Do not provide any thing else in the final summary. Just the brief summary along with hypothesis and provide a concise output without skipping important information.
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
        string title,
        string summary,
        string status = "loading",
        bool isFinal = false)
    {
        try
        {
            var existingMessage = await _repository.GetMessageAsync(threadId, messageId);

            if (existingMessage == null) return;

            //// Update message text
            string updatedText = ChatMessageService.AppendInvestigationSummary(
                existingMessage.Text, title, summary, status: status, isFinal: isFinal);

            Message updatedMessage = existingMessage with { Text = updatedText };

            await _repository.UpdateMessageAsync(threadId, updatedMessage);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error updating progress message");
        }
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
            "AnalyzeApplicationHealth" => "Analyzing Application Health",
            "AnalyzeActivityLogs" => "Analyzing Activity Logs",
            "AnalyzeConnectedComponents" => "Analyzing Connected Components",
            "AnalyzeLogQueries" => "Analyzing Log Queries",
            "AnalyzeResourceMetrics" => "Analyzing Resource Metrics",
            "AnalyzeGenericLogQueries" => "Analyzing Generic Log Queries",
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
}
