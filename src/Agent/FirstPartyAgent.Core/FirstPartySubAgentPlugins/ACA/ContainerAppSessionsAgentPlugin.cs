// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core;
using Agent.Runtime.MetaAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppSessionsAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.FirstPartySubAgentPlugins.ACA;

[WorkflowClass]
public class ContainerAppSessionsAgentPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly ContainerAppSessionsAgentFactory _ContainerAppSessionsAgentFactory;

    public Guid? ThreadId { get; set; }

    public ContainerAppSessionsAgentPlugin(
           DurableTaskClient durableTaskClient,
           ContainerAppSessionsAgentFactory factory,
           ILogger<ContainerAppSessionsAgent> logger)

    {
        _ContainerAppSessionsAgentFactory = factory;
        _durableTaskClient = durableTaskClient;

    }


    [KernelFunction("list_containerapp_sessions_issue_workflows")]
    [WorkflowFunction]
    [Description("List the information of started workflows for investigating container app sessions issue")]
    public async Task<IReadOnlyList<WorkflowMetadata<ContainerAppSessionsAgentActivityInput>>> ListContainerAppSessionsAgentWorkflows()
    {
        var list = new List<WorkflowMetadata<ContainerAppSessionsAgentActivityInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var input = _ContainerAppSessionsAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
            list.Add(new WorkflowMetadata<ContainerAppSessionsAgentActivityInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: input));
        }

        return list;
    }

    [WorkflowFunction]
    [KernelFunction("start_containerapp_sessions_issue_workflow")]
    [Description("Start the workflow to investigate container app sessions issue")]
    public async Task<string> StartContainerAppSessionsAgentWorkflowAsync(
        [Description("Inputs for Container App Sessions Agent")] ContainerAppSessionsAgentActivityInput input)
    {
        if (ThreadId == null)
        {
            throw new InvalidOperationException("ThreadId must be set before start orchestration.");
        }

        var instanceId = await _ContainerAppSessionsAgentFactory.StartOrchestration(input, ThreadId.Value);
        return $"A workflow has been started to investigate container app sessions issues, the workflow instance id is: {instanceId}";
    }
}
