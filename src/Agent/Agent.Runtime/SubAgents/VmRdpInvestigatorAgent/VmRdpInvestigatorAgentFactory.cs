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

    public const string OrchestrationInstanceIdPrefix = nameof(VmRdpInvestigatorAgentFactory);

    public VmRdpInvestigatorAgentFactory(
        ToolsRepository toolsRepository,
        DurableTaskClient durableTaskClient,
        IArmPlugin armPlugin,
        IAzureSupportCenterPlugin supportCenterPlugin,
        IRecordActionsPlugin recordActionsPlugin,
        INSGRulePlugin nsgRulePlugin)
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
        toolSignatures.Add(ToolsRepository.GetSignature(() => armPluginDefinition.PowerOnVirtualMachine));
        toolSignatures.Add(ToolsRepository.GetSignature(() => armPluginDefinition.GetVirtualMachineBootDiagnostics));

        var nsgRulePluginDefinition = new NSGRulePluginDefinition(nsgRulePlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => nsgRulePluginDefinition.GetNSGRules));
        toolSignatures.Add(ToolsRepository.GetSignature(() => nsgRulePluginDefinition.CreateOrUpdateNSGRuleAsync));
        toolSignatures.Add(ToolsRepository.GetSignature(() => nsgRulePluginDefinition.RemoveNSGRuleAsync));

        //var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
        //toolSignatures.Add(ToolsRepository.GetSignature(() => approvalPluginDefinition.StartApprovalFlow));

        var recordActionsPluginDefinition = new RecordActionsPluginDefinition(recordActionsPlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => recordActionsPluginDefinition.RecordAction));
        toolSignatures.Add(ToolsRepository.GetSignature(() => recordActionsPluginDefinition.GetActionDetails));

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
