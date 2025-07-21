// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Runtime.SubAgents.ManagedIdentityMigration;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Agent.Core.Models.Api.v1;


namespace Agent.Runtime.MetaAgent;

// [Export]
public class ManagedIdentityMigrationPlugin : IMetaAgentManagedIdentityMigrationPlugin
{
    private readonly DurableTaskClient _durableTaskClient;
    private readonly ManagedIdentityMigrationAgentFactory _managedIdentityMigrationAgentFactory;
    private readonly ILogger<ManagedIdentityMigrationPlugin> _logger;

    public Guid? ThreadId { get; set; }


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
            var agentInput = instance.ReadInputAs<ManagedIdentityMigrationAgentInput>();
            if (agentInput != null)
            {
                list.Add(new WorkflowMetadata<ManagedIdentityMigrationInput>(
                    WorkflowInstanceId: instance.InstanceId,
                    Input: agentInput.Input));
            }
        }

        return list;
    }

    [KernelFunction("start_managed_identity_migration_workflow")]
    [Description("Start the workflow to migrate multiple apps to use managed identity when connecting to Azure SQL.")]
    public async Task<string> StartManagedIdentityMigrationAgent(
        [Description("The list of apps to be migrated")] ManagedIdentityMigrationInput input)
    {
        if (ThreadId == null)
        {
            throw new InvalidOperationException("ThreadId must be set before start orchestration.");
        }
        var instanceId = await _managedIdentityMigrationAgentFactory.StartOrchestration(input, ThreadId.Value);
        return $"A workflow has been started to migrate managed identity, the workflow instance id is: {instanceId}";
    }
}

