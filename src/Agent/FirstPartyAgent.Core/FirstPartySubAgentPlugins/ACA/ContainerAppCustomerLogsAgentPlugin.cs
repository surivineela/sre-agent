// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core;
using Agent.Runtime.MetaAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppCustomerLogsAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.FirstPartySubAgentPlugins.ACA;

[WorkflowClass]
public class ContainerAppCustomerLogsAgentPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly ContainerAppCustomerLogsAgentFactory _ContainerAppCustomerLogsAgentFactory;

    public Guid? ThreadId { get; set; }

    public ContainerAppCustomerLogsAgentPlugin(
        DurableTaskClient durableTaskClient,
        ContainerAppCustomerLogsAgentFactory factory,
        ILogger<ContainerAppCustomerLogsAgent> logger)

    {
        _ContainerAppCustomerLogsAgentFactory = factory;
        _durableTaskClient = durableTaskClient;

    }


    [KernelFunction("list_containerapp_logs_workflow")]
    [WorkflowFunction]
    [Description("List the information of started workflows for investigating container app missing logs issue")]
    public async Task<IReadOnlyList<WorkflowMetadata<ContainerAppCustomerLogsAgentActivityInput>>> ListContainerAppCustomerLogsAgentWorkflows()
    {
        var list = new List<WorkflowMetadata<ContainerAppCustomerLogsAgentActivityInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var input = _ContainerAppCustomerLogsAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
            list.Add(new WorkflowMetadata<ContainerAppCustomerLogsAgentActivityInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: input));
        }

        return list;
    }

    [WorkflowFunction]
    [KernelFunction("start_containerapp_logs_workflow")]
    [Description("Start the workflow to investigate container app missing logs issue")]
    public async Task<string> StartCustomerLogsAgentWorkflowAsync(
        [Description("Inputs for Container App Customer Logs Agent")] ContainerAppCustomerLogsAgentActivityInput input)
    {
        if (ThreadId == null)
        {
            throw new InvalidOperationException("ThreadId must be set before start orchestration.");
        }

        var instanceId = await _ContainerAppCustomerLogsAgentFactory.StartOrchestration(input, ThreadId.Value);
        return $"A workflow has been started to investigate container app missing Logs issues, the workflow instance id is: {instanceId}";
    }
}
