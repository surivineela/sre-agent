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
    private readonly IToolsRepository _toolsRepository;
    private readonly IThreadOrchestrationManager _mappingManager;

    public const string OrchestrationInstanceIdPrefix = nameof(ManagedIdentityMigrationAgent);

    public ManagedIdentityMigrationAgentFactory(
        IMetricsPlugin metricsPlugin,
        IArmPlugin armPlugin,
        ITimePlugin timePlugin,
        IMIConfigurationCheckPlugin miMigrationPlugin,
        IAppIdentityUpdatePlugin appIdentityUpdatePlugin,
        IGithubWorkflowTriggerPlugin githubWorkflowTriggerPlugin,
        IThreadOrchestrationManager mappingManager,
        IToolsRepository toolsRepository,
        DurableTaskClient durableTaskClient)
    {
        _toolsRepository = toolsRepository;
        var toolSignatures = new List<string>();
        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => metricsPluginDefinition.GetSuccessfulRequestVolumeAsync));

        var timePluginDefinition = new TimePluginDefinition(timePlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => timePluginDefinition.GetCurrentUtcTime));
        toolSignatures.Add(_toolsRepository.GetSignature(() => timePluginDefinition.GetAppTimeZone));

        var miMigrationPluginDefinition = new MIConfigurationCheckPluginDefinition(miMigrationPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => miMigrationPluginDefinition.CheckSqlConnectionTypeAsync));
        toolSignatures.Add(_toolsRepository.GetSignature(() => miMigrationPluginDefinition.CheckSqlResourceIdForAppAsync));

        var appIdentityUpdatePluginDefinition = new AppIdentityUpdatePluginDefinition(appIdentityUpdatePlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => appIdentityUpdatePluginDefinition.GetAppManagedIdentityAsync));
        toolSignatures.Add(_toolsRepository.GetSignature(() => appIdentityUpdatePluginDefinition.MigrateWebAppConnStr2ManagedIdentityAsync));
        toolSignatures.Add(_toolsRepository.GetSignature(() => appIdentityUpdatePluginDefinition.EnableSqlAdEntraAdminAsync));

        var githubWorkflowTriggerPluginDefinition = new GithubWorkflowTriggerPluginDefinition(githubWorkflowTriggerPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => githubWorkflowTriggerPluginDefinition.CheckPullRequestMergeStatus));
        toolSignatures.Add(_toolsRepository.GetSignature(() => githubWorkflowTriggerPluginDefinition.TriggerWorkflow));
        toolSignatures.Add(_toolsRepository.GetSignature(() => githubWorkflowTriggerPluginDefinition.TrackWorkflow));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
        _mappingManager = mappingManager;
    }

    public async Task<string> StartOrchestration(
        ManagedIdentityMigrationInput input,
        Guid threadId)
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

        await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);

        return await _durableTaskClient.ScheduleNewManagedIdentityMigrationAgentInstanceAsync(
            new ManagedIdentityMigrationAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: instanceId));
    }

    public ManagedIdentityMigrationInput DeserializeInput(string serializedOrchestraionInput)
    {
        return JsonSerializer.Deserialize<ManagedIdentityMigrationAgentInput>(serializedOrchestraionInput).ThrowIfNull().Input;
    }
}

