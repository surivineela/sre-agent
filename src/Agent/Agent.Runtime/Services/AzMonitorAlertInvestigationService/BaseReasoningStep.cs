// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Logging;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Microsoft.Extensions.Logging;
namespace Agent.Runtime.Services.AzMonitorAlertInvestigation;

/// <summary>
/// Base class for reasoning steps to reduce boilerplate
/// </summary>
public abstract class BaseReasoningStep : IReasoningStep
{
    protected readonly IThreadRepository _repository;
    protected readonly ILogger _logger;
    protected BaseReasoningStep(IThreadRepository repository, ILogger logger)
    {
        _repository = repository;
        _logger = logger;
    }
    public abstract string StepName { get; }
    public abstract int DefaultPriority { get; }
    public abstract Task<StepResult> ExecuteAsync(
        AlertItem alert,
        InvestigationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Helper method to add a reasoning message to the thread
    /// </summary>
    protected async Task AddReasoningMessageAsync(Guid agentContextId, string description, string content)
    {
        _logger.LogInternalInformation($"Adding reasoning message for {StepName} with description {description}");
        await _repository.CreateReasoningMessageAsync(new ReasoningMessage(
            Guid.NewGuid(),
            agentContextId,
            ReasoningMessageRoleEnum.System,
            JsonSerializer.Serialize(new
            {
                description,
                content
            })
        ));
        // Todo: Add reasoning message to chat history? 
    }

    /// <summary>
    /// Format alert details as a prompt
    /// </summary>
    protected string GetAlertInfoAsPrompt(AlertItem alert)
    {
        if (alert == null)
        {
            return "Alert information unavailable";
        }
        var essentials = alert.Properties?.Essentials;
        return $@"Azure Monitor Alert Context:
                ID: {alert.Id ?? "Unknown"}
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

