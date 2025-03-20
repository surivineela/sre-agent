using Agent.Core.Models;
using Agent.Core;
using Microsoft.DurableTask.Client;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Agent.Runtime.SubAgents.ContainerAppsRemediation;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.MetaAgent;

public class ContainerAppsRemediationPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly ContainerAppsRemediationAgentFactory _containerAppsRemediationAgentFactory;
    private readonly ILogger<AppServiceRemediationPlugin> _logger;

    public ContainerAppsRemediationPlugin(
        DurableTaskClient durableTaskClient,
        ContainerAppsRemediationAgentFactory containerAppsRemediationAgentFactory,
        ILogger<AppServiceRemediationPlugin> logger)
    {
        _durableTaskClient = durableTaskClient;
        _containerAppsRemediationAgentFactory = containerAppsRemediationAgentFactory;
        _logger = logger;
    }

    [KernelFunction("list_containerapps_remediation_workflow")]
    [Description("List the information of started workflow for azure container apps app remediation")]
    public async Task<IReadOnlyList<WorkflowMetadata<string>>> ListContainerAppsRemediationWorkflows()
    {
        try
        {
            var list = new List<WorkflowMetadata<string>>();
            await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
                new OrchestrationQuery(
                    Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                    FetchInputsAndOutputs: true)))
            {
                var input = _containerAppsRemediationAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
                list.Add(new WorkflowMetadata<string>(
                    WorkflowInstanceId: instance.InstanceId,
                    Input: input));
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list Container Apps remediation workflows.");
            return [];
        }
    }

    [KernelFunction("start_container_apps_remediation_workflow")]
    [Description("Start the workflow to remediate azure container apps for memory leak, network issues, app issues etc")]
    public async Task<string> StartContainerAppsRemediationAgent(
        [Description("The list of complete Azure Resource Id of the apps having the issue and a description of the problem")] string input,
        string threadId)
    {

        try
        {
            var instanceId = await _containerAppsRemediationAgentFactory.StartOrchestration(input, threadId);
            return $"A workflow has been started to remediate container apps, the workflow instance id is: {instanceId}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start container apps remediation workflow.");
            return $"Failed to start container apps remediation workflow.";
        }
    }
}
