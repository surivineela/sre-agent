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
        _toolsRegistry.RegisterPlugin<IncidentPluginDefinition>();

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
        _toolsRegistry.RegisterTool<GitHubIssuePluginDefinition>(x => x.FetchGithubIssue);
        _toolsRegistry.RegisterTool<GitHubIssuePluginDefinition>(x => x.FindConnectedRepo);

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

        diagnosticAgentTools.RegisterReadOnlyPlugin<KubePluginDefinition>();

        var diagnosticAgent = new DiagnosisAgentInput
        {
            ToolSignatures = diagnosticAgentTools.ToolSignatures,
            CustomInstructions = """
            The provided tools are specialized for retrieving information about an AKS cluster. Use them to investigate the issue.
            Be deeply aware of the following concepts in kubernetes:
            1. All resources live in some namespace. Usually they only interact with resources in the same namespace.
            2. If no namespace was mentioned, check ALL namespaces to find the workloads which are most likely to be relevant.
            3. Workloads in kubernetes are deployed in a hierarchy: Pod → ReplicaSet → Deployment / StatefulSet / DaemonSet.

            When checking workload revision history, pay very close attention to the differences between each revision.
            Container images may be different, environment variables may have been added, removed, or changed.

            When checking workload environment variables, the Deployment object is the source of truth about the current configuration.
            Replicasets may be running old configurations. The Deployment spec is the current configuration.
            """
        };

        return [diagnosticAgent];
    }
}
