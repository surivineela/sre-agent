using System.ComponentModel;
using Agent.Core.Models;
using Agent.Runtime.SubAgents.TlsBestPractices;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Agent.Runtime.MetaAgent;

// [Export]
public class TlsBestPracticesPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly TlsBestPracticeAgentFactory _tlsBestPracticeAgentFactory;
    private readonly ILogger<TlsBestPracticesPlugin> _logger;

    public string? ThreadId { get; set; }

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
            // workaround because above fetch inputs and outputs is not working
            var instanceWithInput = await _durableTaskClient.GetInstanceAsync(instance.InstanceId, getInputsAndOutputs: true);
            var agentInput = instanceWithInput.ReadInputAs<TlsBestPracticesAgentInput>();
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
        var instanceId = await _tlsBestPracticeAgentFactory.StartOrchestration(input, ThreadId);
        return $"A workflow has been started to adopt tls best practice, the workflow instance id is: {instanceId}";
    }
}
