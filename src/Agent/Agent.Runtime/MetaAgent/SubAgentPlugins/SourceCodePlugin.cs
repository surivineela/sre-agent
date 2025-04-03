// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Runtime.SubAgents.SourceCodeAgent;
using Agent.Core.Models.Api.v1;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Agent.Runtime.MetaAgent;

// [Export]
public class SourceCodePlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly SourceCodeAgentFactory _sourceCodeAgentFactory;
    private readonly ILogger<SourceCodePlugin> _logger;

    public ThreadContext? Context { get; set; }

    public SourceCodePlugin(
        DurableTaskClient durableTaskClient,
        SourceCodeAgentFactory sourceCodeAgentFactory,
        ILogger<SourceCodePlugin> logger)
    {
        _durableTaskClient = durableTaskClient;
        _sourceCodeAgentFactory = sourceCodeAgentFactory;
        _logger = logger;
    }

    [KernelFunction("list_source_code_workflow")]
    [Description("List the information of started source code workflow")]
    public async Task<IReadOnlyList<WorkflowMetadata<SourceCodeInput>>> ListSourceCodeWorkflows()
    {
        var list = new List<WorkflowMetadata<SourceCodeInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                InstanceIdPrefix: SourceCodeAgentFactory.OrchestrationInstanceIdPrefix,
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            // workaround because above fetch inputs and outputs is not working
            var instanceWithInput = await _durableTaskClient.GetInstanceAsync(instance.InstanceId, getInputsAndOutputs: true);
            var agentInput = instanceWithInput.ReadInputAs<SourceCodeAgentInput>();
            list.Add(new WorkflowMetadata<SourceCodeInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: agentInput.Input));
        }

        return list;
    }

    [KernelFunction("start_source_code_workflow")]
    [Description("Start the workflow to add source code nodes to container app nodes. You will receive GitHub repo urls and Azure container app resource ids and you wil ")]
    public async Task<string> StartSourceCodeAgent(
        [Description("The list of apps that need source code nodes")] SourceCodeInput input)
    {
        if (Context == null)
        {
            throw new InvalidOperationException("ThreadContext must be set before start orchestration.");
        }
        var instanceId = await _sourceCodeAgentFactory.StartOrchestration(input, Context);
        return $"A workflow has been started to adopt tls best practice, the workflow instance id is: {instanceId}";
    }
}

