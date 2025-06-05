using System.Text.Json;
using Agent.Core;
using Agent.Plugins;
using Microsoft.DurableTask.Client;
using Agent.Core.Models.Api.v1;
using Microsoft.DurableTask;
using Agent.Plugins.Definitions;
using OperationalAgentCore;

namespace Agent.Runtime.SubAgents.FunctionAppConnectivityAgent;
public sealed class FunctionAppConnectivityAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IToolsRepository _toolsRepository;

    public const string OrchestrationInstanceIdPrefix = nameof(FunctionAppConnectivityAgentFactory);

    public FunctionAppConnectivityAgentFactory(
           IToolsRepository toolsRepository,
           DurableTaskClient durableTaskClient,
           IArmPlugin armPlugin,
           IRoleAssignmentPlugin roleAssignmentPlugin,
           IGraphDBPlugin graphDBPlugin
           )
    {
        _toolsRepository = toolsRepository;
        var toolSignatures = new List<string>();

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(_toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        var armPluginDefinition = new ArmPluginDefinition(armPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => armPluginDefinition.GetArmResourceAsJson));
        toolSignatures.Add(_toolsRepository.GetSignature(() => armPluginDefinition.CheckConnectivityToAzureWebJobsStorage));
        toolSignatures.Add(_toolsRepository.GetSignature(() => armPluginDefinition.CheckTcpConnectivity));
        toolSignatures.Add(_toolsRepository.GetSignature(() => armPluginDefinition.CheckDnsResolution));
        toolSignatures.Add(_toolsRepository.GetSignature(() => armPluginDefinition.GetAppSetting));
        toolSignatures.Add(_toolsRepository.GetSignature(() => armPluginDefinition.ListKeysAndUpdateAppSettingsAsync));

        var roleAssignmentPluginDefinition = new RoleAssignmentPluginDefinition(roleAssignmentPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => roleAssignmentPluginDefinition.AddRoleAssignment));
        toolSignatures.Add(_toolsRepository.GetSignature(() => roleAssignmentPluginDefinition.CheckRoleAssignment));
        toolSignatures.Add(_toolsRepository.GetSignature(() => roleAssignmentPluginDefinition.GetRoleDetailsFromNameAsync));

        var graphDBPluginDefinition = new GraphDBPluginDefinition(graphDBPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => graphDBPluginDefinition.GetResourceIdForResourceName));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
    }

    public async Task<string> StartOrchestration(
       string functionAppResourceId,
       Guid threadId)
    {
        return await _durableTaskClient.ScheduleNewFunctionAppConnectivityAgentInstanceAsync(
            new FunctionAppConnectivityAgentInput(
                FunctionAppResourceId: functionAppResourceId,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    public FunctionAppConnectivityAgentInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<FunctionAppConnectivityAgentInput>(serializedOrchestrationInput).ThrowIfNull();
    }

}
