// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Logging;
using Agent.Runtime.Models;
using Microsoft.Extensions.Logging;
namespace Agent.Runtime.Services.AzMonitorAlertInvestigation;

//TODO: Split into separate classes once working
/// <summary>
/// Step for analyzing application health metrics
/// </summary>
public class ApplicationHealthStep : BaseReasoningStep
{
    private readonly IAzMonitorAlertInvestigationService _service;
    public override string StepName => "ApplicationHealth";
    public override int DefaultPriority => 1; // Highest priority step
    public ApplicationHealthStep(
        IAzMonitorAlertInvestigationService service,
        IThreadRepository repository,
        ILogger<ApplicationHealthStep> logger) : base(repository, logger)
    {
        _service = service;
    }
    public override async Task<StepResult> ExecuteAsync(
        AlertItem alert,
        InvestigationContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var thread = await _repository.GetThreadAsync(context.ThreadId);
            var result = await _service.GetApplicationHealthAsync(alert, thread);
            if (!string.IsNullOrEmpty(result))
            {
                await AddReasoningMessageAsync(
                    context.AgentContextId,
                    "Analysis of application health metrics (availability, cpu, memory etc.)",
                    result);
            }
            return new StepResult(
                StepName,
                result,
                !string.IsNullOrEmpty(result) && !result.Contains("Error"));
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error executing {StepName} step");
            return new StepResult(StepName, $"Error: {ex.Message}", false);
        }
    }
}

/// <summary>
/// Step for analyzing activity logs
/// </summary>
public class ActivityLogAnalysisStep : BaseReasoningStep
{
    private readonly IAzMonitorAlertInvestigationService _service;
    public override string StepName => "ActivityLogAnalysis";
    public override int DefaultPriority => 1;
    public ActivityLogAnalysisStep(
        IAzMonitorAlertInvestigationService service,
        IThreadRepository repository,
        ILogger<ActivityLogAnalysisStep> logger) : base(repository, logger)
    {
        _service = service;
    }
    public override async Task<StepResult> ExecuteAsync(
        AlertItem alert,
        InvestigationContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var thread = await _repository.GetThreadAsync(context.ThreadId);
            var result = await _service.AnalyzeActivityLogsForResource(alert, thread);
            if (!string.IsNullOrEmpty(result))
            {
                await AddReasoningMessageAsync(
                    context.AgentContextId,
                    "Analysis of recent activity logs for configuration changes",
                    result);
            }
            return new StepResult(
                StepName,
                result,
                !string.IsNullOrEmpty(result) && !result.Contains("Error"));
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error executing {StepName} step");
            return new StepResult(StepName, $"Error: {ex.Message}", false);
        }
    }
}

/// <summary>
/// Step for analyzing connected components using Knowledge graph.
/// </summary>
public class ConnectedComponentsAnalysisStep : BaseReasoningStep
{
    private readonly IAzMonitorAlertInvestigationService _service;
    public override string StepName => "ConnectedComponentsAnalysis";
    public override int DefaultPriority => 1;
    public ConnectedComponentsAnalysisStep(
        IAzMonitorAlertInvestigationService service,
        IThreadRepository repository,
        ILogger<ConnectedComponentsAnalysisStep> logger) : base(repository, logger)
    {
        _service = service;
    }
    public override async Task<StepResult> ExecuteAsync(
        AlertItem alert,
        InvestigationContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var thread = await _repository.GetThreadAsync(context.ThreadId);
            var result = await _service.AnalyzeConnectedComponents(alert, thread);
            if (!string.IsNullOrEmpty(result))
            {
                await AddReasoningMessageAsync(
                    context.AgentContextId,
                    "Analysis of connected components and dependencies",
                    result);
            }
            return new StepResult(
                StepName,
                result,
                !string.IsNullOrEmpty(result) && !result.Contains("Error"));
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error executing {StepName} step");
            return new StepResult(StepName, $"Error: {ex.Message}", false);
        }
    }
}

/// <summary>
/// Step for analyzing log queries saved in Users Log analytics workspace.
/// </summary>
public class LogQueryAnalysisStep : BaseReasoningStep
{
    private readonly IAzMonitorAlertInvestigationService _service;
    public override string StepName => "LogQueryAnalysis";
    public override int DefaultPriority => 1;
    public LogQueryAnalysisStep(
        IAzMonitorAlertInvestigationService service,
        IThreadRepository repository,
        ILogger<LogQueryAnalysisStep> logger) : base(repository, logger)
    {
        _service = service;
    }
    public override async Task<StepResult> ExecuteAsync(
        AlertItem alert,
        InvestigationContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var thread = await _repository.GetThreadAsync(context.ThreadId);
            var result = await _service.AnalyzeLogQueries(alert, thread);
            if (!string.IsNullOrEmpty(result))
            {
                await AddReasoningMessageAsync(
                    context.AgentContextId,
                    "Analysis of log query results for error patterns",
                    result);
            }
            return new StepResult(
                StepName,
                result,
                !string.IsNullOrEmpty(result) && !result.Contains("Error"));
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error executing {StepName} step");
            return new StepResult(StepName, $"Error: {ex.Message}", false);
        }
    }
}

/// <summary>
/// Step for analyzing resource metrics
/// </summary>
public class MetricsAnalysisStep : BaseReasoningStep
{
    private readonly IAzMonitorAlertInvestigationService _service;
    public override string StepName => "MetricsAnalysis";
    public override int DefaultPriority => 1;
    public MetricsAnalysisStep(
        IAzMonitorAlertInvestigationService service,
        IThreadRepository repository,
        ILogger<MetricsAnalysisStep> logger) : base(repository, logger)
    {
        _service = service;
    }
    public override async Task<StepResult> ExecuteAsync(
        AlertItem alert,
        InvestigationContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var thread = await _repository.GetThreadAsync(context.ThreadId);
            var result = await _service.GetMetricsForResource(alert, thread);
            if (!string.IsNullOrEmpty(result))
            {
                await AddReasoningMessageAsync(
                    context.AgentContextId,
                    "Analysis of resource metrics and performance counters",
                    result);
            }
            return new StepResult(
                StepName,
                result,
                !string.IsNullOrEmpty(result) && !result.Contains("Error"));
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error executing {StepName} step");
            return new StepResult(StepName, $"Error: {ex.Message}", false);
        }
    }
}
