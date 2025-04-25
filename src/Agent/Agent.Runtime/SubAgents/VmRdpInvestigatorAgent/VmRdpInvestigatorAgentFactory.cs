using System.Text.Json;
using Agent.Core;
using Agent.Plugins;
using Microsoft.DurableTask.Client;
using Agent.Core.Models.Api.v1;
using Microsoft.DurableTask;
using Agent.Runtime.SubAgents.RdpInvestigatorAgent;
using Agent.Plugins.Definitions;
using OperationalAgentCore;

namespace Agent.Runtime.SubAgents.VmRdpInvestigatorAgent;
public sealed class VmRdpInvestigatorAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IToolsRepository _toolsRepository;

    public const string OrchestrationInstanceIdPrefix = nameof(VmRdpInvestigatorAgentFactory);

    public VmRdpInvestigatorAgentFactory(
        IToolsRepository toolsRepository,
        DurableTaskClient durableTaskClient,
        IArmPlugin armPlugin,
        IAzureSupportCenterPlugin supportCenterPlugin,
        IRecordActionsPlugin recordActionsPlugin,
        INSGRulePlugin nsgRulePlugin)
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
        toolSignatures.Add(_toolsRepository.GetSignature(() => armPluginDefinition.PowerOnVirtualMachine));
        toolSignatures.Add(_toolsRepository.GetSignature(() => armPluginDefinition.GetVirtualMachineBootDiagnostics));

        var nsgRulePluginDefinition = new NSGRulePluginDefinition(nsgRulePlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => nsgRulePluginDefinition.GetNSGRules));
        toolSignatures.Add(_toolsRepository.GetSignature(() => nsgRulePluginDefinition.CreateOrUpdateNSGRuleAsync));
        toolSignatures.Add(_toolsRepository.GetSignature(() => nsgRulePluginDefinition.RemoveNSGRuleAsync));

        var recordActionsPluginDefinition = new RecordActionsPluginDefinition(recordActionsPlugin);
        toolSignatures.Add(_toolsRepository.GetSignature(() => recordActionsPluginDefinition.RecordAction));
        toolSignatures.Add(_toolsRepository.GetSignature(() => recordActionsPluginDefinition.GetActionDetails));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
    }

    public async Task<string> StartOrchestration(
       string virtualMachineResourceId,
       Guid threadId)
    {
        return await _durableTaskClient.ScheduleNewVmRdpInvestigatorAgentInstanceAsync(
            new VmRdpInvestigatorAgentInput(
                VirtualMachineResourceId: virtualMachineResourceId,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    public VmRdpInvestigatorAgentInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<VmRdpInvestigatorAgentInput>(serializedOrchestrationInput).ThrowIfNull();
    }

}
