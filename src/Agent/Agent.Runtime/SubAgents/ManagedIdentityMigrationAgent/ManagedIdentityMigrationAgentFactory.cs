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
        ToolsRepository toolsRepository,
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient)
    {
        var toolSignatures = new List<string>();
        var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => metricsPluginDefinition.GetSuccessfulRequestVolumeAsync));

        var timePluginDefinition = new TimePluginDefinition(timePlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => timePluginDefinition.GetCurrentUtcTime));
        toolSignatures.Add(toolsRepository.GetSignature(() => timePluginDefinition.GetAppTimeZone));

        var miMigrationPluginDefinition = new MIConfigurationCheckPluginDefinition(miMigrationPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => miMigrationPluginDefinition.CheckSqlConnectionTypeAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => miMigrationPluginDefinition.CheckSqlResourceIdForAppAsync));

        var appIdentityUpdatePluginDefinition = new AppIdentityUpdatePluginDefinition(appIdentityUpdatePlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => appIdentityUpdatePluginDefinition.GetAppManagedIdentityAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => appIdentityUpdatePluginDefinition.MigrateWebAppConnStr2ManagedIdentityAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => appIdentityUpdatePluginDefinition.EnableSqlAdEntraAdminAsync));

        var githubWorkflowTriggerPluginDefinition = new GithubWorkflowTriggerPluginDefinition(githubWorkflowTriggerPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => githubWorkflowTriggerPluginDefinition.CheckPullRequestMergeStatus));
        toolSignatures.Add(toolsRepository.GetSignature(() => githubWorkflowTriggerPluginDefinition.TriggerWorkflow));
        toolSignatures.Add(toolsRepository.GetSignature(() => githubWorkflowTriggerPluginDefinition.TrackWorkflow));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));

        var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => approvalPluginDefinition.StartApprovalFlow));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
        _mappingManager = mappingManager;
    }

    public async Task<string> StartOrchestration(
        ManagedIdentityMigrationInput input,
        string threadId = "")
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

        if (threadId != null)
        {
            await _mappingManager.AddMappingAsync(new ThreadOrchestrationMapping(
                Id: $"mapping_{threadId}",
                ThreadId: threadId,
                OrchestrationInstanceId: instanceId,
                CreatedTimestamp: DateTime.UtcNow,
                ModifiedTimestamp: DateTime.UtcNow
                )
            );
        }

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
