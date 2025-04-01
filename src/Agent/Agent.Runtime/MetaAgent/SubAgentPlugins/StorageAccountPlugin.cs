using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents.StorageAccountAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Agent.Runtime.MetaAgent;

// NOTE: It seems to me like all these plugins can be a single generic base class, with overrides
// that just provide descriptions and call the base.
// However, this is only possible is the various factory classes implement a shared interface that
// we can rely on.

public class StorageAccountPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly StorageAccountAgentFactory _storageAccountAgentFactory;
    private readonly ILogger<StorageAccountPlugin> _logger;
    public ThreadContext? Context { get; set; }

    public StorageAccountPlugin(
        DurableTaskClient durableTaskClient,
        StorageAccountAgentFactory factory,
        ILogger<StorageAccountPlugin> logger)
    {
        _durableTaskClient = durableTaskClient;
        _storageAccountAgentFactory = factory;
        _logger = logger;
    }

    [KernelFunction("list_storage_account_workflows")]
    [Description("List the information of started workflows for storage account remediation")]
    public async Task<IReadOnlyList<WorkflowMetadata<StorageAccountAgentPlanInput>>> ListStorageAccountAgentWorkflows()
    {
        var list = new List<WorkflowMetadata<StorageAccountAgentPlanInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                InstanceIdPrefix: StorageAccountAgentFactory.OrchestrationInstanceIdPrefix,
                Statuses: [],
                FetchInputsAndOutputs: true)))
        {
            // workaround because above fetch inputs and outputs is not working
            var instanceWithInput = await _durableTaskClient.GetInstanceAsync(instance.InstanceId, getInputsAndOutputs: true);
            var agentInput = instanceWithInput.ReadInputAs<StorageAccountAgentInput>();

            list.Add(new WorkflowMetadata<StorageAccountAgentPlanInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: agentInput.Input));
        }

        return list;
    }

    [KernelFunction("start_storage_account_workflow")]
    [Description("Start the workflow to apply changes to storage accounts")]
    public async Task<string> StartStorageAccountAgent(
        [Description("The list of complete Azure Resource Id of the storage accounts")] StorageAccountAgentPlanInput input)
    {
        if (Context == null)
        {
            throw new InvalidOperationException("ThreadContext must be set before start orchestration.");
        }
        var instanceId = await _storageAccountAgentFactory.StartOrchestration(input, Context);
        return $"A workflow has been started to adopt tls best practice, the workflow instance id is: {instanceId}";
    }
}
