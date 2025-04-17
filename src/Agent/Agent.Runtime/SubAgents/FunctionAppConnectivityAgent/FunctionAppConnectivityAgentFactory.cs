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

    public const string OrchestrationInstanceIdPrefix = nameof(FunctionAppConnectivityAgentFactory);

    public FunctionAppConnectivityAgentFactory(
        ToolsRepository toolsRepository,
        DurableTaskClient durableTaskClient,
        IArmPlugin armPlugin
        )
    {
        var toolSignatures = new List<string>();

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        var armPluginDefinition = new ArmPluginDefinition(armPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => armPluginDefinition.GetArmResourceAsJson));
        toolSignatures.Add(toolsRepository.GetSignature(() => armPluginDefinition.CheckConnectivity));
        toolSignatures.Add(toolsRepository.GetSignature(() => armPluginDefinition.CheckTcpConnectivity));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
    }

    public async Task<string> StartOrchestration(
       FunctionAppConnectivityAgentInput input,
       ThreadContext context)
    {
        return await _durableTaskClient.ScheduleNewFunctionAppConnectivityAgentInstanceAsync(
            new FunctionAppConnectivityAgentInput(
                FunctionAppResourceId: input.FunctionAppResourceId,
                ToolSignatures: _toolSignatures,
                Context: context),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    public FunctionAppConnectivityAgentInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<FunctionAppConnectivityAgentInput>(serializedOrchestrationInput).ThrowIfNull();
    }

}
