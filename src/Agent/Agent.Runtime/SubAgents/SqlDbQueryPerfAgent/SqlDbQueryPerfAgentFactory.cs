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

    public const string OrchestrationInstanceIdPrefix = nameof(SqlDbQueryPerfAgentFactory);

    public SqlDbQueryPerfAgentFactory(
        DurableTaskClient durableTaskClient,
        IArmPlugin armPlugin,
        IAzureSupportCenterPlugin supportCenterPlugin,
        IRecordActionsPlugin recordActionsPlugin)
    {
        var toolSignatures = new List<string>();

        var supportCenterPluginDefinition = new AzureSupportCenterPluginDefinition(supportCenterPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => supportCenterPluginDefinition.GetSupportProductsFromArm));
        toolSignatures.Add(ToolsRepository.GetSignature(() => supportCenterPluginDefinition.GetSupportProblemClassificationsForProduct));
        toolSignatures.Add(ToolsRepository.GetSignature(() => supportCenterPluginDefinition.GetAzureSupportCenterDiagnosticResultsForQuestion));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        var armPluginDefinition = new ArmPluginDefinition(armPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => armPluginDefinition.GetArmResourceAsJson));

        //var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
        //toolSignatures.Add(ToolsRepository.GetSignature(() => approvalPluginDefinition.StartApprovalFlow));

        var recordActionsPluginDefinition = new RecordActionsPluginDefinition(recordActionsPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => recordActionsPluginDefinition.RecordAction));
        toolSignatures.Add(ToolsRepository.GetSignature(() => recordActionsPluginDefinition.GetActionDetails));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
    }

    public async Task<string> StartOrchestration(
       string azSqlDbResourceId,
       ThreadContext context)
    {
        return await _durableTaskClient.ScheduleNewSqlDbQueryPerfAgentInstanceAsync(
            new SqlDbQueryPerfAgentInput(
                AzSqlDbResourceId: azSqlDbResourceId,
                ToolSignatures: _toolSignatures,
                Context: context),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    public SqlDbQueryPerfAgentInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<SqlDbQueryPerfAgentInput>(serializedOrchestrationInput).ThrowIfNull();
    }
}
