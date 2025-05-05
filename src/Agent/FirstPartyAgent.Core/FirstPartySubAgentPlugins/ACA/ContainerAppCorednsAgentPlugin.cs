// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core;
using Agent.Runtime.MetaAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppCorednsAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.FirstPartySubAgentPlugins.ACA;

// [MENDATORY]
[WorkflowClass]
public class ContainerAppCorednsAgentPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly ContainerAppCorednsAgentFactory _ContainerAppCorednsAgentFactory;

    public Guid? ThreadId { get; set; }

    public ContainerAppCorednsAgentPlugin(
           DurableTaskClient durableTaskClient,
           ContainerAppCorednsAgentFactory factory,
           ILogger<ContainerAppCorednsAgent> logger)

    {
        _ContainerAppCorednsAgentFactory = factory;
        _durableTaskClient = durableTaskClient;

    }


    [KernelFunction("list_containerapp_core_dns_issue_workflows")]
    [WorkflowFunction]
    [Description("List the information of started workflows for investigating container app Core DNS issue")]
    public async Task<IReadOnlyList<WorkflowMetadata<ContainerAppCorednsAgentActivityInput>>> ListContainerAppCoreDNSAgentWorkflows()
    {
        var list = new List<WorkflowMetadata<ContainerAppCorednsAgentActivityInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var input = _ContainerAppCorednsAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
            list.Add(new WorkflowMetadata<ContainerAppCorednsAgentActivityInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: input));
        }

        return list;
    }

    [WorkflowFunction]
    [KernelFunction("start_containerapp_core_dns_issue_workflow")]
    [Description("Start the workflow to investigate container app Core DNS issue")]
    public async Task<string> StartContainerAppCoreDNSAgentWorkflowAsync(
        [Description("Inputs for Container App Core DNS Agent")] ContainerAppCorednsAgentActivityInput input)
    {
        if (ThreadId == null)
        {
            throw new InvalidOperationException("ThreadId must be set before start orchestration.");
        }

        var instanceId = await _ContainerAppCorednsAgentFactory.StartOrchestration(input, ThreadId.Value);
        return $"A workflow has been started to investigate container app core DNS issues, the workflow instance id is: {instanceId}";
    }
}
