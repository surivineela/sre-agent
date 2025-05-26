// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using System.ComponentModel;
using Agent.Core;
using Agent.Runtime.MetaAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppEnvironmentAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.FirstPartySubAgentPlugins.ACA;
[WorkflowClass]
public class ContainerAppEnvironmentAgentPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    public Guid? ThreadId { get; set; }

    private readonly ContainerAppEnvironmentAgentFactory _containerAppEnvironmentAgentFactory;

    public ContainerAppEnvironmentAgentPlugin(
        DurableTaskClient durableTaskClient,
        ContainerAppEnvironmentAgentFactory factory,
        ILogger<ContainerAppEnvironmentAgentFactory> logger)

    {
        _containerAppEnvironmentAgentFactory = factory;
        _durableTaskClient = durableTaskClient;
    }

    [WorkflowFunction]
    [KernelFunction("list_containerapps_environment_workflow")]
    [Description("List the information of started workflow for container apps environment request")]
    public async Task<IReadOnlyList<WorkflowMetadata<ContainerAppEnvironmentAgentActivityInput>>> ListContainerAppEnvironmentAgentWorkflowsAsync()
    {
        var list = new List<WorkflowMetadata<ContainerAppEnvironmentAgentActivityInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var input = _containerAppEnvironmentAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
            list.Add(new WorkflowMetadata<ContainerAppEnvironmentAgentActivityInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: input));
        }

        return list;
    }

    [WorkflowFunction]
    [KernelFunction("start_container_apps_environment_workflow")]
    [Description("Start the workflow to process azure container apps environment request.")]
    public async Task<string> StartContainerAppEnvironmentAgentWorkflowAsync(ContainerAppEnvironmentAgentActivityInput input)
    {
        if (ThreadId == null)
        {
            throw new InvalidOperationException("ThreadId must be set before start orchestration.");
        }

        var instanceId = await _containerAppEnvironmentAgentFactory.StartOrchestration(input, ThreadId.Value);
        return $"A workflow has been started to investigate the container app environment issues, the workflow instance id is: {instanceId}";

    }
}
