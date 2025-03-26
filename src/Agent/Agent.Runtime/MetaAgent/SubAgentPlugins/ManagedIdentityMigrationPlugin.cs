using System.ComponentModel;
using Agent.Core.Models;
using Agent.Runtime.SubAgents.ManagedIdentityMigration;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Agent.Runtime.MetaAgent;

// [Export]
public class ManagedIdentityMigrationPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly ManagedIdentityMigrationAgentFactory _managedIdentityMigrationAgentFactory;
    private readonly ILogger<ManagedIdentityMigrationPlugin> _logger;

    public string? ThreadId { get; set; }


    public ManagedIdentityMigrationPlugin(
        DurableTaskClient durableTaskClient,
        ManagedIdentityMigrationAgentFactory managedIdentityMigrationAgentFactory,
        ILogger<ManagedIdentityMigrationPlugin> logger)
    {
        _durableTaskClient = durableTaskClient;
        _managedIdentityMigrationAgentFactory = managedIdentityMigrationAgentFactory;
        _logger = logger;
    }

    [KernelFunction("list_managed_identity_migration_workflow")]
    [Description("List the information of started managed identity migration workflow")]
    public async Task<IReadOnlyList<WorkflowMetadata<ManagedIdentityMigrationInput>>> ListManagedIdentityMigrations()
    {
        var list = new List<WorkflowMetadata<ManagedIdentityMigrationInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                InstanceIdPrefix: ManagedIdentityMigrationAgentFactory.OrchestrationInstanceIdPrefix,
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            // workaround because above fetch inputs and outputs is not working
            var instanceWithInput = await _durableTaskClient.GetInstanceAsync(instance.InstanceId, getInputsAndOutputs: true);
            var agentInput = instanceWithInput.ReadInputAs<ManagedIdentityMigrationAgentInput>();
            list.Add(new WorkflowMetadata<ManagedIdentityMigrationInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: agentInput.Input));
        }

        return list;
    }

    [KernelFunction("start_managed_identity_migration_workflow")]
    [Description("Start the workflow to migrate multiple apps to use managed identity when connecting to Azure SQL.")]
    public async Task<string> StartManagedIdentityMigrationAgent(
        [Description("The list of apps to be migrated")] ManagedIdentityMigrationInput input)
    {
        var instanceId = await _managedIdentityMigrationAgentFactory.StartOrchestration(input, ThreadId);
        return $"A workflow has been started to migrate managed identity, the workflow instance id is: {instanceId}";
    }
}
