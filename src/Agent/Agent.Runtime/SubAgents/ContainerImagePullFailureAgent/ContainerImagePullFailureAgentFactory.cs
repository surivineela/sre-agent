using Agent.Plugins;
using Agent.Plugins.Definitions;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask;
using System.Text.Json;
using Agent.Core;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.Communication;

namespace Agent.Runtime.SubAgents.ContainerImagePullFailureAgent;

public sealed class ContainerImagePullFailureAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IThreadOrchestrationManager _mappingManager;

    public const string OrchestrationInstanceIdPrefix = nameof(ContainerImagePullFailureAgent);

    public ContainerImagePullFailureAgentFactory(
        IMetricsPlugin metricsPlugin,
        IApprovalPlugin approvalPlugin,
        ITimePlugin timePlugin,
        IRemediationPlugin remediationPlugin,
        IRecordActionsPlugin recordActionsPlugin,
        IChartPlugin chartPlugin,
        IContainerAppPlugin containerAppPlugin,
        IContainerImagePullFailurePlugin containerImagePullFailurePlugin,
        ToolsRepository toolsRepository,
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient)
    {
        var toolSignatures = new List<string>();
        var timePluginDefinition = new TimePluginDefinition(timePlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => timePluginDefinition.GetCurrentUtcTime));
        toolSignatures.Add(ToolsRepository.GetSignature(() => timePluginDefinition.GetAppTimeZone));

        // Registry verification tools
        var containerImagePullFailurePluginDefinition = new ContainerImagePullFailurePluginDefinition(containerImagePullFailurePlugin);
        toolSignatures.Add(ToolsRepository.GetSignature(() => containerImagePullFailurePluginDefinition.CheckAcrAuthentication));
        toolSignatures.Add(ToolsRepository.GetSignature(() => containerImagePullFailurePluginDefinition.VerifyExternalRegistry));
        toolSignatures.Add(ToolsRepository.GetSignature(() => containerImagePullFailurePluginDefinition.CheckImagePulling));
        toolSignatures.Add(ToolsRepository.GetSignature(() => containerImagePullFailurePluginDefinition.GetImageReferenceFromResourceId));
        toolSignatures.Add(ToolsRepository.GetSignature(() => containerImagePullFailurePluginDefinition.GetNetworkSecurityRulesForResource));
        toolSignatures.Add(ToolsRepository.GetSignature(() => containerImagePullFailurePluginDefinition.IsAzureContainerRegistryImageAccessibleAsync));
        toolSignatures.Add(ToolsRepository.GetSignature(() => containerImagePullFailurePluginDefinition.RollbackToLastWorkingImage));
        toolSignatures.Add(ToolsRepository.GetSignature(() => containerImagePullFailurePluginDefinition.UpdateContainerImage));
        toolSignatures.Add(ToolsRepository.GetSignature(() => containerImagePullFailurePluginDefinition.RetryImagePull));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(ToolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
        _mappingManager = mappingManager;
    }

    public async Task<string> StartOrchestration(
        string resourceId,
        Guid threadId)
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

        await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);

        return await _durableTaskClient.ScheduleNewContainerImagePullFailureAgentInstanceAsync(
            new ContainerImagePullFailureAgentInput(
                ResourceId: resourceId,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: instanceId));
    }

    public ContainerImagePullFailureInput DeserializeInput(string serializedOrchestrationInput)
    {
        try
        {
            var agentInput = JsonSerializer.Deserialize<ContainerImagePullFailureAgentInput>(serializedOrchestrationInput).ThrowIfNull();
            return new ContainerImagePullFailureInput(resourceId: agentInput.ResourceId);
        }
        catch (JsonException ex)
        {
            // Handle the exception, e.g., log the error or return a default value
            throw new InvalidOperationException("Failed to deserialize input.", ex);
        }
    }
}
