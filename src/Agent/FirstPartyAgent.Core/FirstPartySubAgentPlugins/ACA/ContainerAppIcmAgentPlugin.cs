// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core;
using Agent.Runtime.MetaAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppIcmAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.FirstPartySubAgentPlugins.ACA;

// [MENDATORY]
[WorkflowClass]
public class ContainerAppIcmAgentPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly ContainerAppIcmAgentFactory _ContainerAppIcmAgentFactory;

    public Guid? ThreadId { get; set; }

    public ContainerAppIcmAgentPlugin(
           DurableTaskClient durableTaskClient,
           ContainerAppIcmAgentFactory factory,
           ILogger<ContainerAppIcmAgent> logger)

    {
        _ContainerAppIcmAgentFactory = factory;
        _durableTaskClient = durableTaskClient;

    }


    [KernelFunction("list_containerapp_icm_workflows")]
    [WorkflowFunction]
    [Description("List the information of started workflows for Interacting with IcM")]
    public async Task<IReadOnlyList<WorkflowMetadata<ContainerAppIcmAgentActivityInput>>> ListContainerAppIcmAgentWorkflows()
    {
        var list = new List<WorkflowMetadata<ContainerAppIcmAgentActivityInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var input = _ContainerAppIcmAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
            list.Add(new WorkflowMetadata<ContainerAppIcmAgentActivityInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: input));
        }

        return list;
    }

    [WorkflowFunction]
    [KernelFunction("start_containerapp_icm_workflow")]
    [Description("Start the workflow to interacting with IcM")]
    public async Task<string> StartContainerAppIcmAgentWorkflowAsync(
        [Description("Inputs for Container App IcM Agent")] ContainerAppIcmAgentActivityInput input)
    {
        if (ThreadId == null)
        {
            throw new InvalidOperationException("ThreadId must be set before start orchestration.");
        }

        var instanceId = await _ContainerAppIcmAgentFactory.StartOrchestration(input, ThreadId.Value);
        return $"A workflow has been started to interact with IcM, the workflow instance id is: {instanceId}";
    }
}
