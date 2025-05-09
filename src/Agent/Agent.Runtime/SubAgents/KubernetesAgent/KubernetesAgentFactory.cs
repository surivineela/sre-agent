// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core;
using Agent.Core.Interfaces;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.Communication;
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
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: instanceId));
    }

    public string DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<KubernetesAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }
}
