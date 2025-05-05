// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core;
using Agent.Runtime.MetaAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.RevisionAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.FirstPartySubAgentPlugins.ACA;

[WorkflowClass]
public class ContainerAppRevisionAgentPlugin 
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly ContainerAppRevisionAgentFactory _containerAppRevisionAgentFactory;

    public Guid? ThreadId { get; set; }

    public ContainerAppRevisionAgentPlugin(
           DurableTaskClient durableTaskClient,
           ContainerAppRevisionAgentFactory factory,
           ILogger<ContainerAppRevisionAgent> logger)
           
    {
        _containerAppRevisionAgentFactory = factory;
        _durableTaskClient = durableTaskClient;

    }

    
    [KernelFunction("list_container_app_revision_workflows")]
    [WorkflowFunction]
    [Description("List the information of started workflows for container app revision resources remediation")]
    public async Task<IReadOnlyList<WorkflowMetadata<ContainerAppRevisionAgentActivityInput>>> ListRevisionAgentWorkflows()
    {
        var list = new List<WorkflowMetadata<ContainerAppRevisionAgentActivityInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var input = _containerAppRevisionAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
            list.Add(new WorkflowMetadata<ContainerAppRevisionAgentActivityInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: input));
        }

        return list;
    }

    [WorkflowFunction]
    [KernelFunction("start_container_app_revision_workflow")]
    [Description("Start the workflow to apply changes to container app revision resource")]
    public async Task<string> StartRevisionAgentWorkflowAsync(
        [Description("the resource id of the app service resource to be fixed")] ContainerAppRevisionAgentActivityInput resourceId)
    {
        if (ThreadId == null)
        {
            throw new InvalidOperationException("ThreadId must be set before start orchestration.");
        }

        var instanceId = await _containerAppRevisionAgentFactory.StartOrchestration(resourceId, ThreadId.Value);
        return $"A workflow has been started to fix revision issues, the workflow instance id is: {instanceId}";
    }
}

