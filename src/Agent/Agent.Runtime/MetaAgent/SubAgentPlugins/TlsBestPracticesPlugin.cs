// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.TlsBestPractices;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;


namespace Agent.Runtime.MetaAgent;

// [Export]
public class TlsBestPracticesPlugin : IMetaAgentTlsBestPracticesPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly TlsBestPracticeAgentFactory _tlsBestPracticeAgentFactory;
    private readonly ILogger<TlsBestPracticesPlugin> _logger;

    public ThreadContext? Context { get; set; }

    public TlsBestPracticesPlugin(
        DurableTaskClient durableTaskClient,
        TlsBestPracticeAgentFactory tlsBestPracticeAgentFactory,
        ILogger<TlsBestPracticesPlugin> logger)
    {
        _durableTaskClient = durableTaskClient;
        _tlsBestPracticeAgentFactory = tlsBestPracticeAgentFactory;
        _logger = logger;
    }

    [KernelFunction("list_tls_best_practice_workflow")]
    [Description("List the information of started tls best practice workflow")]
    public async Task<IReadOnlyList<WorkflowMetadata<TlsBestPracticesInput>>> ListTlsBestPracticeWorkflows()
    {
        var list = new List<WorkflowMetadata<TlsBestPracticesInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                InstanceIdPrefix: TlsBestPracticeAgentFactory.OrchestrationInstanceIdPrefix,
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var agentInput = instance.ReadInputAs<TlsBestPracticesAgentInput>();
            list.Add(new WorkflowMetadata<TlsBestPracticesInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: agentInput.Input));
        }

        return list;
    }

    [KernelFunction("start_tls_best_practice_workflow")]
    [Description("Start the workflow to migrate multiple apps to adopt tls best practice.")]
    public async Task<string> StartTlsBestPracticeAgent(
        [Description("The list of apps to be migrated")] TlsBestPracticesInput input)
    {
        if (Context == null)
        {
            throw new InvalidOperationException("ThreadContext must be set before start orchestration.");
        }
        var instanceId = await _tlsBestPracticeAgentFactory.StartOrchestration(input, Context);
        return $"A workflow has been started to adopt tls best practice, the workflow instance id is: {instanceId}";
    }
}

