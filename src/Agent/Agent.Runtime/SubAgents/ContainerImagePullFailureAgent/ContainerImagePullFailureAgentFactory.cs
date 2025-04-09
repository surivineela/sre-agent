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
        ToolsRepository toolsRepository,
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient)
    {
        var toolSignatures = new List<string>();
        var timePluginDefinition = new TimePluginDefinition(timePlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => timePluginDefinition.GetCurrentUtcTime));
        toolSignatures.Add(toolsRepository.GetSignature(() => timePluginDefinition.GetAppTimeZone));

        //var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
        //toolSignatures.Add(toolsRepository.GetSignature(() => metricsPluginDefinition.GetWebAppCpuMetrics));
        //toolSignatures.Add(toolsRepository.GetSignature(() => metricsPluginDefinition.GetMemoryMetrics));
        //toolSignatures.Add(toolsRepository.GetSignature(() => metricsPluginDefinition.GetFunctionAppRequestAvailability));
        //toolSignatures.Add(toolsRepository.GetSignature(() => metricsPluginDefinition.GetSuccessfulRequestVolumeAsync));

        //var chartPluginDefinition = new ChartPluginDefinition(chartPlugin);
        //toolSignatures.Add(toolsRepository.GetSignature(() => chartPluginDefinition.PlotTimeSeriesDataAsync));
        //toolSignatures.Add(toolsRepository.GetSignature(() => chartPluginDefinition.PlotPieChartAsync));
        //toolSignatures.Add(toolsRepository.GetSignature(() => chartPluginDefinition.PlotBarChartAsync));

        //TODO: Uncomment when the methods are implemented
        //var remediationPluginDefinition = new RemediationPluginDefinition(remediationPlugin);
        //toolSignatures.Add(toolsRepository.GetSignature(() => remediationPluginDefinition.GetContainerLogs));
        //toolSignatures.Add(toolsRepository.GetSignature(() => remediationPluginDefinition.UpdateContainerAppSettings));
        //toolSignatures.Add(toolsRepository.GetSignature(() => remediationPluginDefinition.RollbackToLastWorkingImage));
        //toolSignatures.Add(toolsRepository.GetSignature(() => remediationPluginDefinition.VerifyRegistryAccess));

        var recordActionsPluginDefinition = new RecordActionsPluginDefinition(recordActionsPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => recordActionsPluginDefinition.RecordAction));
        toolSignatures.Add(toolsRepository.GetSignature(() => recordActionsPluginDefinition.GetActionDetails));

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.Wait));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.MarkPlanComplete));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.NotifyUser));
        toolSignatures.Add(toolsRepository.GetSignature(() => controlFlowPluginDefinition.AskUserForInput));

        var approvalPluginDefinition = new ApprovalPluginDefinition(approvalPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => approvalPluginDefinition.StartApprovalFlow));

        _toolSignatures = toolSignatures;
        _durableTaskClient = durableTaskClient;
        _mappingManager = mappingManager;
    }

    public async Task<string> StartOrchestration(
        string input,
        string resourceId,
        string imageReference,
        string errorMessage,
        ThreadContext context)
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";
        var threadId = context.ThreadId.ToString();

        await _mappingManager.AddMappingAsync(threadId, instanceId);

        return await _durableTaskClient.ScheduleNewContainerImagePullFailureAgentInstanceAsync(
            new ContainerImagePullFailureAgentInput(
                Input: input,
                ResourceId: resourceId,
                ImageReference: imageReference,
                ErrorMessage: errorMessage,
                ToolSignatures: _toolSignatures,
                Context: context),
            new StartOrchestrationOptions(InstanceId: instanceId));
    }

    public ContainerImagePullFailureInput DeserializeInput(string serializedOrchestrationInput)
    {
        try
        {
            var agentInput = JsonSerializer.Deserialize<ContainerImagePullFailureAgentInput>(serializedOrchestrationInput).ThrowIfNull();
            return new ContainerImagePullFailureInput(
                message: agentInput.Input,
                resourceId: agentInput.ResourceId,
                imageReference: agentInput.ImageReference,
                errorMessage: agentInput.ErrorMessage);
        }
        catch (JsonException ex)
        {
            // Handle the exception, e.g., log the error or return a default value
            throw new InvalidOperationException("Failed to deserialize input.", ex);
        }
    }
}
