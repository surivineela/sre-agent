using Agent.Plugins.Definitions;
using Agent.Plugins;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
using Agent.Core.Models;
using Agent.Core;
using System.Text.Json;

namespace Agent.Runtime.SubAgents.ManagedIdentityMigration;

public sealed class ManagedIdentityMigrationAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;

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
    }

    public async Task<string> StartOrchestration(
        ManagedIdentityMigrationInput input,
        string threadId = "")
    {
        return await _durableTaskClient.ScheduleNewManagedIdentityMigrationAgentInstanceAsync(
            new ManagedIdentityMigrationAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: $"{nameof(ManagedIdentityMigrationAgent)}-{Guid.NewGuid()}"));
    }

    public ManagedIdentityMigrationInput DeserializeInput(string serializedOrchestraionInput)
    {
        return JsonSerializer.Deserialize<ManagedIdentityMigrationAgentInput>(serializedOrchestraionInput).ThrowIfNull().Input;
    }
}
