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

    [KernelFunction("ListKubernetesAgentWorkflow")]
    [Description("List the information of started workflow for Azure Kubernetes Service related requests")]
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

    [KernelFunction("StartKubernetesAgentWorkflow")]
    [Description("Start the workflow to handle any requests related to AKS (Azure Kubernetes Service), e.g. check status of AKS cluster or workloads deployed on it, diagnose AKS workload issues, etc.")]
    public async Task<string> StartKubernetesAgentWorkflow(
        [Description("Detailed summarization of the request that wanted to be delegated to the Azure Kubernetes Service SRE Agent to handle, all context information are required especially for subscription ID, resource group and AKS cluster name.")] string input)
    {
        if (ThreadId == null)
        {
            throw new InvalidOperationException("ThreadId must be set before start orchestration.");
        }
        var instanceId = await _kubernetesAgentFactory.StartOrchestration(input, ThreadId.Value);
        return $"Azure Kubernetes Service SRE Agent is figuring out the request, all following user input will be handled directly until request completed. Orchestration instance id to this request is: {instanceId}, thread id is: {ThreadId}.";
    }
}

