using System.Text.Json;
using Agent.Core;
using Agent.Core.Models.Api.v1;
using Agent.Plugins.Definitions;
using Agent.Plugins;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;


namespace Agent.Runtime.SubAgents.SqlDbQueryPerfAgent;
public sealed class SqlDbQueryPerfAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IToolsRepository _toolsRepository;

    public const string OrchestrationInstanceIdPrefix = nameof(SqlDbQueryPerfAgentFactory);

    public SqlDbQueryPerfAgentFactory(
        IToolsRepository toolsRepository,
        DurableTaskClient durableTaskClient,
        IArmPlugin armPlugin,
        IAzureSupportCenterPlugin supportCenterPlugin,
        IRecordActionsPlugin recordActionsPlugin)
    {
        _toolsRepository = toolsRepository;
        var toolSignatures = new List<string>();

        var supportCenterPluginDefinition = new AzureSupportCenterPluginDefinition(supportCenterPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => supportCenterPluginDefinition.GetSupportProductsFromArm));
        toolSignatures.Add(_toolsRepository.GetSignature(() => supportCenterPluginDefinition.GetSupportProblemClassificationsForProduct));
        toolSignatures.Add(_toolsRepository.GetSignature(() => supportCenterPluginDefinition.GetAzureSupportCenterDiagnosticResultsForQuestion));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        var armPluginDefinition = new ArmPluginDefinition(armPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => armPluginDefinition.GetArmResourceAsJson));

        var recordActionsPluginDefinition = new RecordActionsPluginDefinition(recordActionsPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => recordActionsPluginDefinition.RecordAction));
        toolSignatures.Add(_toolsRepository.GetSignature(() => recordActionsPluginDefinition.GetActionDetails));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
    }

    public async Task<string> StartOrchestration(
       string azSqlDbResourceId,
       Guid threadId)
    {
        return await _durableTaskClient.ScheduleNewSqlDbQueryPerfAgentInstanceAsync(
            new SqlDbQueryPerfAgentInput(
                AzSqlDbResourceId: azSqlDbResourceId,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    public SqlDbQueryPerfAgentInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<SqlDbQueryPerfAgentInput>(serializedOrchestrationInput).ThrowIfNull();
    }
}
