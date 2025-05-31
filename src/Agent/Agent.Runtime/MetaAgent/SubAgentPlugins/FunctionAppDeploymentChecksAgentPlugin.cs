using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Agent.Logging;
using Agent.Runtime.SubAgents.FunctionAppDeploymentChecksAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Agent.Runtime.MetaAgent;

/// <summary>
/// Plugin for starting the Function App Deployment Checks Agent
/// </summary>
public class FunctionAppDeploymentChecksAgentPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly FunctionAppDeploymentChecksAgentFactory _functionAppDeploymentChecksAgentFactory;
    private readonly ILogger<FunctionAppDeploymentChecksAgentPlugin> _logger;

    public Guid? ThreadId { get; set; }

    public FunctionAppDeploymentChecksAgentPlugin(
        DurableTaskClient durableTaskClient,
        FunctionAppDeploymentChecksAgentFactory functionAppDeploymentChecksAgentFactory,
        ILogger<FunctionAppDeploymentChecksAgentPlugin> logger)
    {
        _durableTaskClient = durableTaskClient;
        _functionAppDeploymentChecksAgentFactory = functionAppDeploymentChecksAgentFactory;
        _logger = logger;
    }

    [KernelFunction("check_function_app_deployment")]
    [Description("Start the workflow to investigate deployment information and issues in an Azure Function app")]
    public async Task<string> StartFunctionAppDeploymentChecksAgent(
        [Description("ARM resource id for the Function app to investigate")] string functionAppResourceId)
    {
        if (ThreadId == null)
        {
            _logger.LogInternalError("Thread context is not set. Please set the context before starting the workflow.");
            throw new InvalidOperationException("Thread context is not set. Please set the context before starting the workflow.");
        }

        try
        {
            _logger.LogInternalInformation("Starting Function App Deployment Checks agent for {ResourceId}", functionAppResourceId);
            var instanceId = await _functionAppDeploymentChecksAgentFactory.StartOrchestration(functionAppResourceId, ThreadId.Value);
            return $"A workflow has been started to investigate deployment information for your Function app: {instanceId}";
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to start the orchestration for deployment investigation for {ResourceId}", functionAppResourceId);
            throw new InvalidOperationException("Failed to start the orchestration for deployment investigation.", ex);
        }
    }
}
