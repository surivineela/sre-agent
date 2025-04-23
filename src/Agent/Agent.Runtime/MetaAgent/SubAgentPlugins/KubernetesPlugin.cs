// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.KubernetesAgent;


namespace Agent.Runtime.MetaAgent;

public class KubernetesAgentPlugin : IMetaAgentKubernetesAgentPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly KubernetesAgentFactory _kubernetesAgentFactory;
    private readonly ILogger<KubernetesAgentPlugin> _logger;

    public Guid? ThreadId { get; set; }

    public KubernetesAgentPlugin(
        DurableTaskClient durableTaskClient,
        KubernetesAgentFactory kubernetesAgentFactory,
        ILogger<KubernetesAgentPlugin> logger)
    {
        _durableTaskClient = durableTaskClient;
        _kubernetesAgentFactory = kubernetesAgentFactory;
        _logger = logger;
    }

    [KernelFunction("list_kubernetes_agent_workflow")]
    [Description("List the information of started workflow for azure kubernetes service")]
    public async Task<IReadOnlyList<WorkflowMetadata<string>>> ListKubernetesAgentWorkflow()
    {
        var list = new List<WorkflowMetadata<string>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                InstanceIdPrefix: KubernetesAgentFactory.OrchestrationInstanceIdPrefix,
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var agentInput = instance.ReadInputAs<KubernetesAgentInput>();

            list.Add(new WorkflowMetadata<string>(
                WorkflowInstanceId: instance.InstanceId,
                Input: agentInput.Input));
        }

        return list;
    }

    [KernelFunction("start_kubernetes_agent_workflow")]
    [Description("Start the workflow for queries related to azure kubernetes service")]
    public async Task<string> StartKubernetesAgentWorkflow(
        [Description("The list of complete Kubernetes workloads having the issue and a description of the problem")] string input)
    {
        if (ThreadId == null)
        {
            throw new InvalidOperationException("ThreadId must be set before start orchestration.");
        }
        var instanceId = await _kubernetesAgentFactory.StartOrchestration(input, ThreadId.Value);
        return $"A workflow has been started to answer Kubernetes related questions or remediate Kubernetes workloads, the workflow instance id is: {instanceId}, thread id is: {ThreadId}.";
    }
}

