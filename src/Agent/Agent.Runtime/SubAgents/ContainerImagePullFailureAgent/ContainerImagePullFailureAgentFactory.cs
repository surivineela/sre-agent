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
        IContainerImagePullFailurePlugin containerRegistryVerificationPlugin,
        ToolsRepository toolsRepository,
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient)
    {
        var toolSignatures = new List<string>();
        var timePluginDefinition = new TimePluginDefinition(timePlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => timePluginDefinition.GetCurrentUtcTime));
        toolSignatures.Add(toolsRepository.GetSignature(() => timePluginDefinition.GetAppTimeZone));

        // Registry verification tools
        var containerImagePullFailurePluginDefinition = new ContainerImagePullFailurePluginDefinition(containerRegistryVerificationPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => containerImagePullFailurePluginDefinition.CheckAcrAuthentication));
        toolSignatures.Add(toolsRepository.GetSignature(() => containerImagePullFailurePluginDefinition.VerifyExternalRegistry));
        toolSignatures.Add(toolsRepository.GetSignature(() => containerImagePullFailurePluginDefinition.CheckImagePulling));
        toolSignatures.Add(toolsRepository.GetSignature(() => containerImagePullFailurePluginDefinition.GetImageReferenceFromResourceId));
        toolSignatures.Add(toolsRepository.GetSignature(() => containerImagePullFailurePluginDefinition.GetNetworkSecurityRulesForResource));
        toolSignatures.Add(toolsRepository.GetSignature(() => containerImagePullFailurePluginDefinition.IsAzureContainerRegistryImageAccessibleAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => containerImagePullFailurePluginDefinition.RollbackToLastWorkingImage));
        toolSignatures.Add(toolsRepository.GetSignature(() => containerImagePullFailurePluginDefinition.UpdateContainerImage));
        toolSignatures.Add(toolsRepository.GetSignature(() => containerImagePullFailurePluginDefinition.RetryImagePull));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
        _mappingManager = mappingManager;
    }

    public async Task<string> StartOrchestration(
        string resourceId,
        ThreadContext context)
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";
        var threadId = context.ThreadId.ToString();

        await _mappingManager.AddMappingAsync(threadId, instanceId);

        return await _durableTaskClient.ScheduleNewContainerImagePullFailureAgentInstanceAsync(
            new ContainerImagePullFailureAgentInput(
                ResourceId: resourceId,
                ToolSignatures: _toolSignatures,
                Context: context),
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
