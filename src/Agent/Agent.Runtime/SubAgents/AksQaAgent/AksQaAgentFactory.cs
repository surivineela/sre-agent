using System.Text.Json;
using Agent.Core;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Agent.Runtime.SubAgents.AksQaAgent;
using Agent.Runtime.Communication;
using Agent.Runtime.HelperAgents;
using Agent.Runtime;

public sealed class AksQaAgentFactory
{
    private readonly AgentToolsRegistry _toolsRegistry;
    private readonly DurableTaskClient _durableTaskClient;

    public const string OrchestrationInstanceIdPrefix = nameof(AksQaAgent);

    public AksQaAgentFactory(DurableTaskClient durableTaskClient)
    {
        _toolsRegistry = new AgentToolsRegistry();
        // Register relevant plugins/tools for AKS QA investigation
        _toolsRegistry.RegisterPlugin<TimePluginDefinition>();
        _toolsRegistry.RegisterPlugin<KubePluginDefinition>();
        _toolsRegistry.RegisterPlugin<ChartPluginDefinition>();
        _toolsRegistry.RegisterPlugin<RecordActionsPluginDefinition>();
        _toolsRegistry.RegisterPlugin<ControlFlowPluginDefinition>();
        _toolsRegistry.RegisterPlugin<PagerDutyIncidentPluginDefinition>();
        _toolsRegistry.RegisterTool<GraphDBPluginDefinition>(x => x.ListSubscriptions);
        _toolsRegistry.RegisterTool<GraphDBPluginDefinition>(x => x.ListResourceGroups);
        _toolsRegistry.RegisterTool<GraphDBPluginDefinition>(x => x.SearchResourceByName);
        _toolsRegistry.RegisterTool<GraphDBPluginDefinition>(x => x.ListResourcesByType);
        _toolsRegistry.RegisterTool<GraphDBPluginDefinition>(x => x.VisualizeAKSMicroserviceTopology);
        _toolsRegistry.RegisterTool<GraphDBPluginDefinition>(x => x.GetResourceBasicProperties);
        _toolsRegistry.RegisterTool<GraphDBPluginDefinition>(x => x.GetResourceIdForResourceName);
        _toolsRegistry.RegisterTool<NSGRulePluginDefinition>(x => x.CreateOrUpdateNSGRuleAsync);
        _toolsRegistry.RegisterTool<NSGRulePluginDefinition>(x => x.RemoveNSGRuleAsync);
        _toolsRegistry.RegisterTool<GitHubIssuePluginDefinition>(x => x.CreateGithubIssue);
        _toolsRegistry.RegisterTool<GitHubIssuePluginDefinition>(x => x.FetchGithubIssue);
        _toolsRegistry.RegisterTool<GitHubIssuePluginDefinition>(x => x.FindConnectedGitHubRepo);
        _toolsRegistry.RegisterTool<HelperAgentsPluginDefinition>(x => x.StartDiagnosisAgent);
        _durableTaskClient = durableTaskClient;
    }

    public async Task<string> StartOrchestration(
        string input,
        Guid threadId)
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{threadId}-{DateTime.Now:yyyyMMdd-HHmmss}";
        return await _durableTaskClient.ScheduleNewAksQaAgentInstanceAsync(
            new AksQaAgentInput(
                Input: input,
                ToolSignatures: _toolsRegistry.ToolSignatures,
                ThreadId: threadId),
            new StartOrchestrationOptions(InstanceId: instanceId));
    }

    public string DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<AksQaAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }
}
