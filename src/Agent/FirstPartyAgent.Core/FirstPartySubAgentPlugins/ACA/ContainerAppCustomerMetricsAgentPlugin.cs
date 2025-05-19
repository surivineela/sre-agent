using Agent.Runtime.MetaAgent;
using Microsoft.Extensions.Logging;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppCustomerMetricsAgent;
using Microsoft.DurableTask.Client;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Agent.Core;

namespace FirstPartyAgent.Core.FirstPartySubAgentPlugins.ACA;

[WorkflowClass]
public class ContainerAppCustomerMetricsAgentPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly ContainerAppCustomerMetricsAgentFactory _containerAppCustomerMetricsAgentFactory;

    public Guid? ThreadId { get; set; }

    public ContainerAppCustomerMetricsAgentPlugin (
        DurableTaskClient durableTaskClient,
        ContainerAppCustomerMetricsAgentFactory containerAppCustomerMetricsAgentFactory,
        ILogger<ContainerAppCustomerMetricsAgentPlugin> logger)
    {
        _containerAppCustomerMetricsAgentFactory = containerAppCustomerMetricsAgentFactory;
        _durableTaskClient = durableTaskClient;
    }

    [KernelFunction("list_containerapp_missing_metrics_issue_workflows")]
    [WorkflowFunction]
    [Description("List the information of started workflows for investigating container app Missing Metrics issue")]
    public async Task<IReadOnlyList<WorkflowMetadata<ContainerAppCustomerMetricsAgentActivityInput>>> ListContainerAppCustomerMetricsAgentWorkflows()
    {
        var list = new List<WorkflowMetadata<ContainerAppCustomerMetricsAgentActivityInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var input = _containerAppCustomerMetricsAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
            list.Add(new WorkflowMetadata<ContainerAppCustomerMetricsAgentActivityInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: input));
        }

        return list;
    }

    [WorkflowFunction]
    [KernelFunction("start_containerapp_missing_metrics_issue_workflow")]
    [Description("Start the workflow to investigate container app Missing Metrics issue")]
    public async Task<string> StartContainerAppMissingMetricsAgentWorkflowAsync(
        [Description("Inputs for Container App Missing Metrics Agent")] ContainerAppCustomerMetricsAgentActivityInput input)
    {
        if (ThreadId == null)
        {
            throw new InvalidOperationException("ThreadId must be set before start orchestration.");
        }

        var instanceId = await _containerAppCustomerMetricsAgentFactory.StartOrchestration(input, ThreadId.Value);
        return $"A workflow has been started to investigate container app missing metrics issues, the workflow instance id is: {instanceId}";
    }
}
