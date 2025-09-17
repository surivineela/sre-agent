// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Data.DataModels.IncidentModel;
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
    public override string StepName => "AnalyzeApplicationHealth";
    public override int DefaultPriority => 4;

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
            if (thread == null)
            {
                _logger.LogInternalWarning($"Agent thread is null, threadId: {context.ThreadId}");
                throw new InvalidOperationException($"Agent thread is null: {context.ThreadId}");
            }
            var result = await _service.AnalyzeApplicationHealth(alert, thread);

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
    public override string StepName => "AnalyzeActivityLogs";
    public override int DefaultPriority => 4;

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
            if (thread == null)
            {
                _logger.LogInternalWarning($"Agent thread is null, threadId: {context.ThreadId}");
                throw new InvalidOperationException($"Agent thread is null: {context.ThreadId}");
            }
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
    public override string StepName => "AnalyzeConnectedComponents";
    public override int DefaultPriority => 3;

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
            if (thread == null)
            {
                _logger.LogInternalWarning($"Agent thread is null, threadId: {context.ThreadId}");
                throw new InvalidOperationException($"Agent thread is null: {context.ThreadId}");
            }
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
    public override string StepName => "AnalyzeLogQueries";
    public override int DefaultPriority => 2;

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
            if (thread == null)
            {
                _logger.LogInternalWarning($"Agent thread is null, threadId: {context.ThreadId}");
                throw new InvalidOperationException($"Agent thread is null: {context.ThreadId}");
            }
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
/// Step for executing generic queries for resources that don't have saved queries.
/// </summary>
public class LogQueriesGenericAnalysisStep : BaseReasoningStep
{
    private readonly IAzMonitorAlertInvestigationService _service;

    public override string StepName => "AnalyzeGenericLogQueries";

    public override int DefaultPriority => 2;

    public LogQueriesGenericAnalysisStep(IAzMonitorAlertInvestigationService service,
        IThreadRepository repository,
        ILogger<LogQueriesGenericAnalysisStep> logger) : base(repository, logger)
    {
        _service = service;
    }

    public override async Task<StepResult> ExecuteAsync(AlertItem alert, InvestigationContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var thread = await _repository.GetThreadAsync(context.ThreadId);
            if (thread == null)
            {
                _logger.LogInternalWarning($"Agent thread is null, threadId: {context.ThreadId}");
                throw new InvalidOperationException($"Agent thread is null: {context.ThreadId}");
            }
            var result = await _service.AnalyzeGenericLogQueries(alert, thread);

            if (!string.IsNullOrEmpty(result))
            {
                await AddReasoningMessageAsync(
                    context.AgentContextId,
                    "Analysis of generic log queries in Log Analytics Workspace",
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
            return new StepResult(StepName, $"Error {ex.Message}", false);
        }
    }
}

/// <summary>
/// Step for analyzing resource metrics
/// </summary>
public class MetricsAnalysisStep : BaseReasoningStep
{
    private readonly IAzMonitorAlertInvestigationService _service;
    public override string StepName => "AnalyzeResourceMetrics";
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
            if (thread == null)
            {
                _logger.LogInternalWarning($"Agent thread is null, threadId: {context.ThreadId}");
                throw new InvalidOperationException($"Agent thread is null: {context.ThreadId}");
            }
            var result = await _service.AnalyzeResourceMetrics(alert, thread);

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
