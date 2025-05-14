// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using System.ComponentModel;
using Agent.Core;
using Agent.Runtime.MetaAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppQuotaAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;


namespace FirstPartyAgent.Core.FirstPartySubAgentPlugins.ACA;
[WorkflowClass]
public class ContainerAppQuotaAgentPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    public Guid? ThreadId { get; set; }
    private readonly ContainerAppQuotaAgentFactory _containerAppQuotaAgentFactory;
    public ContainerAppQuotaAgentPlugin(
        DurableTaskClient durableTaskClient,
        ContainerAppQuotaAgentFactory factory,
        ILogger<ContainerAppQuotaAgentFactory> logger)
        
    {
        _containerAppQuotaAgentFactory = factory;
        _durableTaskClient = durableTaskClient;
    }
    [WorkflowFunction]
    [KernelFunction("list_containerapps_quota_workflow")]
    [Description("List the information of started workflow for container apps quota request")]
    public async Task<IReadOnlyList<WorkflowMetadata<ContainerAppQuotaAgentActivityInput>>> ListQuotaAgentWorkflowsAsync()
    {
        var list = new List<WorkflowMetadata<ContainerAppQuotaAgentActivityInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var input = _containerAppQuotaAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
            list.Add(new WorkflowMetadata<ContainerAppQuotaAgentActivityInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: input));
        }

        return list;
    }
    [WorkflowFunction]

    [KernelFunction("start_container_apps_quota_workflow")]
    [Description("Start the workflow to process azure container apps quota request.")]
    public async Task<string> StartQuotaAgentWorkflowAsync(ContainerAppQuotaAgentActivityInput input)
    {
        if (ThreadId == null)
        {
            throw new InvalidOperationException("ThreadId must be set before start orchestration.");
        }

        var instanceId = await _containerAppQuotaAgentFactory.StartOrchestration(input, ThreadId.Value);
        return $"A workflow has been started to fix the apps facing Quota issues, the workflow instance id is: {instanceId}";

    }
}
