// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core;
using Agent.Runtime.MetaAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppIngressAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppQuotaAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.RevisionAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.FirstPartySubAgentPlugins.ACA;
[WorkflowClass]
public class ContainerAppIngressAgentPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly ContainerAppIngressAgentFactory _ContainerAppIngressAgentFactory;
    public Guid? ThreadId { get; set; }
    public ContainerAppIngressAgentPlugin(
        DurableTaskClient durableTaskClient,
        ContainerAppIngressAgentFactory factory,
        ILogger<ContainerAppIngressAgent> logger)
        
    {
        _ContainerAppIngressAgentFactory = factory;
        _durableTaskClient = durableTaskClient;
    }
    [WorkflowFunction]
    [KernelFunction("list_container_app_envoy_workflows")]
    [Description("List the information of started workflows for container app envoy issue investigation.")]
    public async Task<IReadOnlyList<WorkflowMetadata<ContainerAppIngressAgentActivityInput>>> ListEnvoyAgentWorkflowsAsync()
    {
        var list = new List<WorkflowMetadata<ContainerAppIngressAgentActivityInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var input = _ContainerAppIngressAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
            list.Add(new WorkflowMetadata<ContainerAppIngressAgentActivityInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: input));
        }

        return list;
    }
    [WorkflowFunction]
    [KernelFunction("start_container_app_envoy_workflow")]
    [Description("Start the workflow to investigate container app envoy issue.")]
    public async Task<string> StartEnvoyAgentWorkflowAsync(ContainerAppIngressAgentActivityInput input)
    {
        if (ThreadId == null)
        {
            throw new InvalidOperationException("ThreadId must be set before start orchestration.");
        }

        var instanceId = await _ContainerAppIngressAgentFactory.StartOrchestration(input, ThreadId.Value);
        return $"A workflow has been started to fix the apps envoy issues, the workflow instance id is: {instanceId}";

    }
}
