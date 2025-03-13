using Agent.Core.Models;
using Agent.Core;
using Microsoft.DurableTask.Client;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Agent.Runtime.SubAgents.TlsBestPractices;

namespace Agent.Runtime.MetaAgent;

// [Export]
// TODO: we can make this a generic class, we need to make agent factory generic first
public class TlsBestPracticesPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly TlsBestPracticeAgentFactory _tlsBestPracticeAgentFactory;

    public TlsBestPracticesPlugin(
        DurableTaskClient durableTaskClient,
        TlsBestPracticeAgentFactory tlsBestPracticeAgentFactory)
    {
        _durableTaskClient = durableTaskClient;
        _tlsBestPracticeAgentFactory = tlsBestPracticeAgentFactory;
    }

    [KernelFunction("list_tls_best_practice_workflow")]
    [Description("List the information of started tls best practice workflow")]
    public async Task<IReadOnlyList<WorkflowMetadata<TlsBestPracticesInput>>> ListTlsBestPracticeWorkflows()
    {
        var list = new List<WorkflowMetadata<TlsBestPracticesInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var input = _tlsBestPracticeAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
            list.Add(new WorkflowMetadata<TlsBestPracticesInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: input));
        }

        return list;
    }

    [KernelFunction("summarize_tls_best_practice_workflow")]
    [Description("Summarize the status of a started tls best practice workflow")]
    public async Task<WorkflowMetadata<TlsBestPracticesInput>?> SummarizeTlsBestPractice(
        string instanceId)
    {
        var orche = await _durableTaskClient.GetInstanceAsync(instanceId);
        if (orche is null)
        {
            return null;
        }

        // TODO: how to get the chathistory of subagent and summarize a string output here
        return new WorkflowMetadata<TlsBestPracticesInput>(
            WorkflowInstanceId: instanceId,
            Input: _tlsBestPracticeAgentFactory.DeserializeInput(orche.SerializedInput.ThrowIfNull()));
    }

    [KernelFunction("start_tls_best_practice_workflow")]
    [Description("Start the workflow to migrate multiple apps to adopt tls best practice.")]
    public async Task<string> StartTlsBestPracticeAgent(
        [Description("The list of apps to be migrated")] TlsBestPracticesInput input,
        string threadId)
    {

        var instanceId = await _tlsBestPracticeAgentFactory.StartOrchestration(input, threadId);
        return $"A workflow has been started to adopt tls best practice, the workflow instance id is: {instanceId}";
    }
}
