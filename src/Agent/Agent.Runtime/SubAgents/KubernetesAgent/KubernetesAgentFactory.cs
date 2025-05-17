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

            SRE common pattern:
            * Confirm the issue by checking metrics and logs.
            * Check the changes happened at the time of the issue or before. Always suggest to revert the changes if the time of the issue is very close to the time of the change.
              - Changes include but not limited to:
                - Resource it self has a configuration changes (including image, env, args, scaling, etc)
                - Related object has a change (including but not limited to: service, ingress, configmap, secret, etc)
                - Dependant resource has a change
                - Environment changes:
                  * Network security group (NSG) rules
            * Think about other possible causes step by step.

            Common AKS issues include:
            - Bad deployment: Check deployment history of the target workload, pay very close attention to the differences between each revision.

            """
        };

        return [diagnosticAgent];
    }
}
