// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core;
using Agent.Core.Models.Api.v1;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.Communication;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace Agent.Runtime.SubAgents.KubernetesAgent;

// [Export]
public sealed class KubernetesAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IThreadOrchestrationManager _mappingManager;

    public const string OrchestrationInstanceIdPrefix = nameof(KubernetesAgent);

    public KubernetesAgentFactory(
        IKubePlugin kubePlugin,
        IArmPlugin armPlugin,
        IApprovalPlugin approvalPlugin,
        ITimePlugin timePlugin,
        IRemediationPlugin remediationPlugin,
        IRecordActionsPlugin recordActionsPlugin,
        IGraphDBPlugin graphDbPlugin,
        IChartPlugin chartPlugin,
        ToolsRepository toolsRepository,
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient)
    {
        var toolSignatures = new List<string>();
        var timePluginDefinition = new TimePluginDefinition(timePlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => timePluginDefinition.GetCurrentUtcTime));
        toolSignatures.Add(toolsRepository.GetSignature(() => timePluginDefinition.GetAppTimeZone));

        var kubePluginDefinition = new KubePluginDefinition(kubePlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => kubePluginDefinition.GetKubeDeploymentsAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => kubePluginDefinition.GetKubeNamespacesAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => kubePluginDefinition.GetAKSClusterResourceIdAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => kubePluginDefinition.GetKubePodsAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => kubePluginDefinition.GetKubeDeploymentSpecStatusAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => kubePluginDefinition.GetKubeDeploymentEventsAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => kubePluginDefinition.RolloutRestartDeploymentAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => kubePluginDefinition.ScaleDeploymentAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => kubePluginDefinition.GetKubePodEventsAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => kubePluginDefinition.GetKubePodLogsAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => kubePluginDefinition.ExecCommandInPodAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => kubePluginDefinition.ListCRDsAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => kubePluginDefinition.ListCustomResourcesAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => kubePluginDefinition.GetCustomResourceYamlAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => kubePluginDefinition.GetPodYamlAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => kubePluginDefinition.GetPodCpuMetricsForDeploymentAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => kubePluginDefinition.GetPodMemoryMetricsForDeploymentAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => kubePluginDefinition.GetRecentlyUpdatedWorkloadsAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => kubePluginDefinition.GetKubeStatefulsetsAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => kubePluginDefinition.GetKubeStatefulsetSpecStatusAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => kubePluginDefinition.GetKubeStatefulSetEventsAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => kubePluginDefinition.ScaleStatefulSetAsync));

        var graphDBPluginDefinition = new GraphDBPluginDefinition(graphDbPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => graphDBPluginDefinition.VisualizeAKSMicroserviceTopology));

        var chartPluginDefinition = new ChartPluginDefinition(chartPlugin);
        toolSignatures.Add(toolsRepository.GetSignature(() => chartPluginDefinition.PlotTimeSeriesDataAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => chartPluginDefinition.PlotPieChartAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => chartPluginDefinition.PlotBarChartAsync));
        toolSignatures.Add(toolsRepository.GetSignature(() => chartPluginDefinition.PlotScatterAsync));

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
        ThreadContext context)
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{context.ThreadId}-{DateTime.Now:yyyyMMdd-HHmmss}";

        var threadId = context.ThreadId.ToString();

        await _mappingManager.AddMappingAsync(threadId, instanceId);
        return await _durableTaskClient.ScheduleNewKubernetesAgentInstanceAsync(
            new KubernetesAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                context),
            new StartOrchestrationOptions(InstanceId: instanceId));
    }

    public string DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<KubernetesAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }
}
