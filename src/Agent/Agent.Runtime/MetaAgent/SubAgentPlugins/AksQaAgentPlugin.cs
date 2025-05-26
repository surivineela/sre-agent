// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.AksQaAgent;


namespace Agent.Runtime.MetaAgent;

public class AksQaAgentPlugin : IMetaAgentAksQaAgentPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly AksQaAgentFactory _AksQaAgentFactory;
    private readonly ILogger<AksQaAgentPlugin> _logger;

    public Guid? ThreadId { get; set; }

    public AksQaAgentPlugin(
        DurableTaskClient durableTaskClient,
        AksQaAgentFactory AksQaAgentFactory,
        ILogger<AksQaAgentPlugin> logger)
    {
        _durableTaskClient = durableTaskClient;
        _AksQaAgentFactory = AksQaAgentFactory;
        _logger = logger;
    }

    [KernelFunction("ListAksQaAgentWorkflow")]
    [Description("List the information of started workflow for Azure Kubernetes Service related requests")]
    public async Task<IReadOnlyList<WorkflowMetadata<string>>> ListAksQaAgentWorkflow()
    {
        var list = new List<WorkflowMetadata<string>>();

        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                InstanceIdPrefix: AksQaAgentFactory.OrchestrationInstanceIdPrefix,
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var agentInput = instance.ReadInputAs<AksQaAgentInput>();

            list.Add(new WorkflowMetadata<string>(
                WorkflowInstanceId: instance.InstanceId,
                Input: agentInput.Input));
        }

        return list;
    }

    [KernelFunction("StartAksQaAgent")]
    [Description("Start the Aks Qa Agent to handle simpler requests related to AKS (Azure Kubernetes Service), such as listing pods, checking API server status, creating deployments, and basic AKS management tasks.")]
    public async Task<string> StartAksQaAgent(
        [Description("Detailed summarization of the request that wanted to be delegated to the Azure Kubernetes Service SRE Agent to handle, all context information are required especially for subscription ID, resource group and AKS cluster name.")] string input)
    {
        if (ThreadId == null)
        {
            throw new InvalidOperationException("ThreadId must be set before start orchestration.");
        }
        var instanceId = await _AksQaAgentFactory.StartOrchestration(input, ThreadId.Value);
        return $"Azure Kubernetes Service SRE Agent is figuring out the request, all following user input will be handled directly until request completed. Orchestration instance id to this request is: {instanceId}, thread id is: {ThreadId}.";
    }
}

