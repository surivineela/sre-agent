using Agent.Core.Models;
using Agent.Core;
using Agent.Runtime.SubAgents.ManagedIdentityMigration;
using Microsoft.DurableTask.Client;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Agent.Runtime.MetaAgent;

// [Export]
public class ManagedIdentityMigrationPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly ManagedIdentityMigrationAgentFactory _managedIdentityMigrationAgentFactory;

    public ManagedIdentityMigrationPlugin(
        DurableTaskClient durableTaskClient,
        ManagedIdentityMigrationAgentFactory managedIdentityMigrationAgentFactory)
    {
        _durableTaskClient = durableTaskClient;
        _managedIdentityMigrationAgentFactory = managedIdentityMigrationAgentFactory;
    }

    [KernelFunction("list_managed_identity_migration_workflow")]
    [Description("List the information of started managed identity migration workflow")]
    public async Task<IReadOnlyList<WorkflowMetadata<ManagedIdentityMigrationInput>>> ListManagedIdentityMigrations()
    {
        var list = new List<WorkflowMetadata<ManagedIdentityMigrationInput>>();
        await foreach (var instance in _durableTaskClient.GetAllInstancesAsync(
            new OrchestrationQuery(
                Statuses: [OrchestrationRuntimeStatus.Pending, OrchestrationRuntimeStatus.Running],
                FetchInputsAndOutputs: true)))
        {
            var input = _managedIdentityMigrationAgentFactory.DeserializeInput(instance.SerializedInput.ThrowIfNull());
            list.Add(new WorkflowMetadata<ManagedIdentityMigrationInput>(
                WorkflowInstanceId: instance.InstanceId,
                Input: input));
        }

        return list;
    }

    [KernelFunction("summarize_managed_identity_migration_workflow")]
    [Description("Summarize the status of a started managed identity migration workflow")]
    public async Task<WorkflowMetadata<ManagedIdentityMigrationInput>?> SummarizeManagedIdentityMigration(
        string instanceId)
    {
        var orche = await _durableTaskClient.GetInstanceAsync(instanceId);
        if (orche is null)
        {
            return null;
        }

        // TODO: how to get the chathistory of subagent and summarize a string output here
        return new WorkflowMetadata<ManagedIdentityMigrationInput>(
            WorkflowInstanceId: instanceId,
            Input: _managedIdentityMigrationAgentFactory.DeserializeInput(orche.SerializedInput.ThrowIfNull()));
    }

    [KernelFunction("start_managed_identity_migration_workflow")]
    [Description("Start the workflow to migrate multiple apps to use managed identity when connecting to Azure SQL.")]
    public async Task<string> StartManagedIdentityMigrationAgent(
        [Description("The list of apps to be migrated")] ManagedIdentityMigrationInput input,
        string threadId)
    {

        var instanceId = await _managedIdentityMigrationAgentFactory.StartOrchestration(input, threadId);
        return $"A workflow has been started to migrate managed identity, the workflow instance id is: {instanceId}";
    }
}
