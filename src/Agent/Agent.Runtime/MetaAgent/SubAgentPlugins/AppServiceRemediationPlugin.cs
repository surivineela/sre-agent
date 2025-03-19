using Agent.Core.Models;
using Agent.Core;
using Microsoft.DurableTask.Client;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Agent.Runtime.SubAgents.AppServiceRemediation;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.MetaAgent;

public class AppServiceRemediationPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly AppServiceRemediationAgentFactory _appServiceRemediationAgentFactory;
    private readonly ILogger<AppServiceRemediationPlugin> _logger;

    public AppServiceRemediationPlugin(
        DurableTaskClient durableTaskClient,
        AppServiceRemediationAgentFactory appServiceRemediationAgentFactory,
        ILogger<AppServiceRemediationPlugin> logger)
    {
        _durableTaskClient = durableTaskClient;
        _appServiceRemediationAgentFactory = appServiceRemediationAgentFactory;
        _logger = logger;
    }

    [KernelFunction("list_app_service_remediation_workflow")]
    [Description("List the information of started workflow for app service/function app remediation")]
    public async Task<IReadOnlyList<WorkflowMetadata<string>>> ListAppServiceRemediationWorkflows()
    {
        try
        {
            var list = new List<WorkflowMetadata<string>>();
            await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
                new OrchestrationQuery(
                    Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                    FetchInputsAndOutputs: true)))
            {
                var input = _appServiceRemediationAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
                list.Add(new WorkflowMetadata<string>(
                    WorkflowInstanceId: instance.InstanceId,
                    Input: input));
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list app service remediation workflows.");
            return [];
        }
    }

    [KernelFunction("summarize_app_service_remediation_workflow")]
    [Description("Summarize the status of a started app service remediation workflow")]
    public async Task<WorkflowMetadata<string>?> SummarizeAppServiceRemidiationWorkflow(
        string instanceId)
    {
        try
        {
            var orche = await _durableTaskClient.GetInstanceAsync(instanceId);
            if (orche is null)
            {
                return null;
            }

            // TODO: how to get the chathistory of subagent and summarize a string output here
            return new WorkflowMetadata<string>(
                WorkflowInstanceId: instanceId,
                Input: _appServiceRemediationAgentFactory.DeserializeInput(orche.SerializedInput.ThrowIfNull()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to summarize app service remediation workflow.");
            return null;
        }
    }

    [KernelFunction("start_app_service_remediation_workflow")]
    [Description("Start the workflow to migrate multiple apps to use managed identity when connecting to Azure SQL.")]
    public async Task<string> StartAppServiceRemediationAgent(
        [Description("The list of complete Azure Resource Id of the apps having the issue and a description of the problem")] string input,
        string threadId)
    {

        try
        {
            var instanceId = await _appServiceRemediationAgentFactory.StartOrchestration(input, threadId);
            return $"A workflow has been started to remediate app service, the workflow instance id is: {instanceId}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start app service remediation workflow.");
            return $"Failed to start app service remediation workflow.";
        }
    }
}
