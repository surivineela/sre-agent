// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.Communication;
using Agent.Runtime.HelperAgents;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;

namespace Agent.Runtime.SubAgents.KubernetesAgent;

// [Export]
public sealed class KubernetesAgentFactory
{
    private readonly AgentToolsRegistry _toolsRegistry;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IThreadOrchestrationManager _mappingManager;

    public const string OrchestrationInstanceIdPrefix = nameof(KubernetesAgent);

    public KubernetesAgentFactory(
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient)
    {
        _toolsRegistry = new AgentToolsRegistry();
        _toolsRegistry.RegisterPlugin<TimePluginDefinition>();
        _toolsRegistry.RegisterPlugin<KubePluginDefinition>();
        _toolsRegistry.RegisterPlugin<ChartPluginDefinition>();
        _toolsRegistry.RegisterPlugin<RecordActionsPluginDefinition>();
        _toolsRegistry.RegisterPlugin<ControlFlowPluginDefinition>();
        _toolsRegistry.RegisterPlugin<PagerDutyIncidentPluginDefinition>();

        _toolsRegistry.RegisterTool<GraphDBPluginDefinition>(x => x.ListSubscriptions);
        _toolsRegistry.RegisterTool<GraphDBPluginDefinition>(x => x.ListResourceGroups);
        _toolsRegistry.RegisterTool<GraphDBPluginDefinition>(x => x.SearchResourceByName);
        _toolsRegistry.RegisterTool<GraphDBPluginDefinition>(x => x.GetActivityLogsSummary);
        _toolsRegistry.RegisterTool<GraphDBPluginDefinition>(x => x.ListResourcesByType);
        _toolsRegistry.RegisterTool<GraphDBPluginDefinition>(x => x.VisualizeAKSMicroserviceTopology);
        _toolsRegistry.RegisterTool<GraphDBPluginDefinition>(x => x.GetResourceBasicProperties);
        _toolsRegistry.RegisterTool<GraphDBPluginDefinition>(x => x.GetResourceIdForResourceName);

        _toolsRegistry.RegisterTool<NSGRulePluginDefinition>(x => x.CreateOrUpdateNSGRuleAsync);
        _toolsRegistry.RegisterTool<NSGRulePluginDefinition>(x => x.RemoveNSGRuleAsync);

        _toolsRegistry.RegisterTool<GitHubIssuePluginDefinition>(x => x.CreateGithubIssue);
        _toolsRegistry.RegisterTool<GitHubIssuePluginDefinition>(x => x.FetchGithubIssues);
        _toolsRegistry.RegisterTool<GitHubIssuePluginDefinition>(x => x.FindConnectedGitHubRepo);

        _toolsRegistry.RegisterTool<HelperAgentsPluginDefinition>(x => x.StartDiagnosisAgent);

        _durableTaskClient = durableTaskClient;
        _mappingManager = mappingManager;
    }

    public async Task<string> StartOrchestration(
        string input,
        Guid threadId)
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{threadId}-{DateTime.Now:yyyyMMdd-HHmmss}";

        await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);
        return await _durableTaskClient.ScheduleNewKubernetesAgentInstanceAsync(
            new KubernetesAgentInput(
                Input: input,
                ToolSignatures: _toolsRegistry.ToolSignatures,
                ThreadId: threadId,
                HelperAgentsInputs: GetHelperAgentInputs()),
            new StartOrchestrationOptions(InstanceId: instanceId));
    }

    public string DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<KubernetesAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }

    private static IReadOnlyList<HelperAgentInput> GetHelperAgentInputs()
    {
        var diagnosticAgentTools = new DiagnosisAgentToolsRegistry();

        diagnosticAgentTools.RegisterReadOnlyTool<GraphDBPluginDefinition>(x => x.SearchResourceByName);
        diagnosticAgentTools.RegisterReadOnlyTool<GraphDBPluginDefinition>(x => x.GetActivityLogsSummary);
        diagnosticAgentTools.RegisterReadOnlyTool<GraphDBPluginDefinition>(x => x.GetResourceBasicProperties);
        diagnosticAgentTools.RegisterReadOnlyTool<GraphDBPluginDefinition>(x => x.ListSubscriptions);
        diagnosticAgentTools.RegisterReadOnlyTool<GraphDBPluginDefinition>(x => x.ListResourceGroups);
        diagnosticAgentTools.RegisterReadOnlyTool<GraphDBPluginDefinition>(x => x.ListResourcesByType);
        diagnosticAgentTools.RegisterReadOnlyTool<GraphDBPluginDefinition>(x => x.VisualizeAKSMicroserviceTopology);
        diagnosticAgentTools.RegisterReadOnlyTool<GraphDBPluginDefinition>(x => x.GetResourceIdForResourceName);

        diagnosticAgentTools.RegisterReadOnlyPlugin<KubePluginDefinition>();

        var diagnosticAgent = new DiagnosisAgentInput
        {
            ToolSignatures = diagnosticAgentTools.ToolSignatures,
            CustomInstructions = """
            The provided tools are specialized for retrieving information about an AKS cluster. Use them to investigate the issue.
            Be deeply aware that all concepts and terminologies mentioned in the issue description are all AKS or Kubernetes related.

            AKS SRE common pattern:
            * Troubleshoot systematically by examining each layer of the system, starting from the application and moving outward:
              - First, inspect the application itself and its directly associated Kubernetes resources (such as services, configmaps, secrets, and PVCs).
              - Next, evaluate the underlying infrastructure that supports the application.
                - This includes the hosting node, any dependent infrastructure services (e.g., databases), the AKS control plane, and the cluster's VNET.
              - Finally, consider other applications within the AKS cluster that the primary application depends on.

            * For each layer, investigate from two perspectives: observability data and change history.
              - Observability data not only includes metrics and logs, but also including any kinds of kubernetes object events, spec, status, etc.
              - If change history not directly supported, try to find indirect signals like replicasets as revision to deployment, controllerrevision as revision to statefulset.

            Common AKS issues include:
              - Application code bugs: Deploying untested or faulty code versions that lead to service disruptions. Evidence could from pod logs, pod crashes since recent deployment.
              - Application configuration errors: Incorrect settings, such as wrong port numbers or image versions. Evidence could from pod logs, pod status/events, failed requests since recent deployment.
              - Changes in dependent applications: Modifications in dependencies causing connectivity or functionality issues. Evidence could from pod logs, slow requests, and recent deployments of dependant applications.
              - Network issues: Problems with network connectivity or routing, e.g, wrong NSG rules, ingress/service mis-configuration. Evidence could from error logs, request metrics, and there's recent changes to NSG, ingress or other custom resource changes.
              - Application itself or dependent application resource exhaustion: Increased load or reduced resource allocation causing shortages (e.g., CPU utilization reaching critical levels, memory OOM). Evidence could from pod logs, resource metrics, recent changes or potential leaks.
              - Infrastructure failures: Failures in underlying infrastructure, such as node outages or AKS control plane issues.
            """
        };

        return [diagnosticAgent];
    }
}
