// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Definitions;
using Agent.Plugins;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
using Agent.Core.Models;
using Agent.Core;
using System.Text.Json;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.Communication;

namespace Agent.Runtime.SubAgents.ManagedIdentityMigration;

public sealed class ManagedIdentityMigrationAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IThreadOrchestrationManager _mappingManager;

    public const string OrchestrationInstanceIdPrefix = nameof(ManagedIdentityMigrationAgent);

    public ManagedIdentityMigrationAgentFactory(
        IMetricsPlugin metricsPlugin,
        IArmPlugin armPlugin,
        IApprovalPlugin approvalPlugin,
        ITimePlugin timePlugin,
        IMIConfigurationCheckPlugin miMigrationPlugin,
        IAppIdentityUpdatePlugin appIdentityUpdatePlugin,
        IGithubWorkflowTriggerPlugin githubWorkflowTriggerPlugin,
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient)
    {
        var toolSignatures = new List<string>();
        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => metricsPluginDefinition.GetSuccessfulRequestVolumeAsync));

        var timePluginDefinition = new TimePluginDefinition(timePlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => timePluginDefinition.GetCurrentUtcTime));
        toolSignatures.Add(ToolsRepository.GetSignature(() => timePluginDefinition.GetAppTimeZone));

        var miMigrationPluginDefinition = new MIConfigurationCheckPluginDefinition(miMigrationPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => miMigrationPluginDefinition.CheckSqlConnectionTypeAsync));
        toolSignatures.Add(ToolsRepository.GetSignature(() => miMigrationPluginDefinition.CheckSqlResourceIdForAppAsync));

        var appIdentityUpdatePluginDefinition = new AppIdentityUpdatePluginDefinition(appIdentityUpdatePlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => appIdentityUpdatePluginDefinition.GetAppManagedIdentityAsync));
        toolSignatures.Add(ToolsRepository.GetSignature(() => appIdentityUpdatePluginDefinition.MigrateWebAppConnStr2ManagedIdentityAsync));
        toolSignatures.Add(ToolsRepository.GetSignature(() => appIdentityUpdatePluginDefinition.EnableSqlAdEntraAdminAsync));

        var githubWorkflowTriggerPluginDefinition = new GithubWorkflowTriggerPluginDefinition(githubWorkflowTriggerPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => githubWorkflowTriggerPluginDefinition.CheckPullRequestMergeStatus));
        toolSignatures.Add(ToolsRepository.GetSignature(() => githubWorkflowTriggerPluginDefinition.TriggerWorkflow));
        toolSignatures.Add(ToolsRepository.GetSignature(() => githubWorkflowTriggerPluginDefinition.TrackWorkflow));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => approvalPluginDefinition.StartApprovalFlow));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
        _mappingManager = mappingManager;
    }

    public async Task<string> StartOrchestration(
        ManagedIdentityMigrationInput input,
         ThreadContext context)
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";
        var threadId = context.ThreadId.ToString();

        await _mappingManager.AddMappingAsync(threadId, instanceId);

        return await _durableTaskClient.ScheduleNewManagedIdentityMigrationAgentInstanceAsync(
            new ManagedIdentityMigrationAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                context),
            new StartOrchestrationOptions(InstanceId: instanceId));
    }

    public ManagedIdentityMigrationInput DeserializeInput(string serializedOrchestraionInput)
    {
        return JsonSerializer.Deserialize<ManagedIdentityMigrationAgentInput>(serializedOrchestraionInput).ThrowIfNull().Input;
    }
}

