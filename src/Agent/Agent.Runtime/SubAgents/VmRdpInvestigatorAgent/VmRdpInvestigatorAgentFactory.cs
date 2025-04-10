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
        IRecordActionsPlugin recordActionsPlugin)
    {
        var toolSignatures = new List<string>();

        var supportCenterPluginDefinition = new AzureSupportCenterPluginDefinition(supportCenterPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => supportCenterPluginDefinition.GetSupportProductsFromArm));
        toolSignatures.Add(toolsRepository.GetSignature(() => supportCenterPluginDefinition.GetSupportProblemClassificationsForProduct));
        toolSignatures.Add(toolsRepository.GetSignature(() => supportCenterPluginDefinition.GetAzureSupportCenterDiagnosticResultsForQuestion));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        var armPluginDefinition = new ArmPluginDefinition(armPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => armPluginDefinition.GetArmResourceAsJson));
        toolSignatures.Add(toolsRepository.GetSignature(() => armPluginDefinition.PowerOnVirtualMachine));
        toolSignatures.Add(toolsRepository.GetSignature(() => armPluginDefinition.GetVirtualMachineBootDiagnostics));

        //var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
        //toolSignatures.Add(toolsRepository.GetSignature(() => approvalPluginDefinition.StartApprovalFlow));

        var recordActionsPluginDefinition = new RecordActionsPluginDefinition(recordActionsPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => recordActionsPluginDefinition.RecordAction));
        toolSignatures.Add(toolsRepository.GetSignature(() => recordActionsPluginDefinition.GetActionDetails));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
    }

    public async Task<string> StartOrchestration(
       VmRdpInvestigatorAgentInput input,
       ThreadContext context)
    {
        return await _durableTaskClient.ScheduleNewVmRdpInvestigatorAgentInstanceAsync(
            new VmRdpInvestigatorAgentInput(
                VirtualMachineResourceId: input.VirtualMachineResourceId,
                ToolSignatures: _toolSignatures,
                Context: context),
            new StartOrchestrationOptions(InstanceId: $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}"));
    }

    public VmRdpInvestigatorAgentInput DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<VmRdpInvestigatorAgentInput>(serializedOrchestrationInput).ThrowIfNull();
    }

}
