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

namespace Agent.Runtime.SubAgents.ContainerAppsRemediation;

public sealed class ContainerAppsRemediationAgentFactory
{
    private readonly IReadOnlyList<string> _toolSignatures;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IThreadOrchestrationManager _mappingManager;

    public const string OrchestrationInstanceIdPrefix = nameof(ContainerAppsRemediationAgent);

    public ContainerAppsRemediationAgentFactory(
        IThreadOrchestrationManager mappingManager,
        DurableTaskClient durableTaskClient)
    {
        var registry = new AgentToolsRegistry();

        registry.RegisterPlugin<TimePluginDefinition>();
        registry.RegisterPlugin<ContainerAppPluginDefinition>();
        registry.RegisterTool<NSGRulePluginDefinition>(x => x.GetNSGRules);
        //registry.RegisterTool<NSGRulePluginDefinition>(x => x.CreateOrUpdateNSGRuleAsync);
        registry.RegisterTool<NSGRulePluginDefinition>(x => x.RemoveNSGRuleAsync);
        registry.RegisterPlugin<ChartPluginDefinition>();
        registry.RegisterTool<GraphDBPluginDefinition>(x => x.FindAllNetworkConnectedResources);
        registry.RegisterPlugin<RecordActionsPluginDefinition>();
        registry.RegisterPlugin<ControlFlowPluginDefinition>();
        registry.RegisterTool<GitHubIssuePluginDefinition>(x => x.CreateGithubIssue);
        registry.RegisterTool<GitHubIssuePluginDefinition>(x => x.FindConnectedRepo);

        registry.RegisterTool<HelperAgentsPluginDefinition>(x => x.StartDiagnosisAgent);
        registry.RegisterTool<DiagnosticsPluginDefinition>(x => x.GetCPUAnalysis);
        registry.RegisterPlugin<DiagnosticsPluginDefinition>();

        _toolSignatures = registry.ToolSignatures;

        _durableTaskClient = durableTaskClient;
        _mappingManager = mappingManager;
    }

    public async Task<string> StartOrchestration(
        string input,
        Guid threadId)
    {
        var instanceId = $"{OrchestrationInstanceIdPrefix}-{Guid.NewGuid()}";

        await _mappingManager.AddMappingAsync(threadId.ToString(), instanceId);
        return await _durableTaskClient.ScheduleNewContainerAppsRemediationAgentInstanceAsync(
            new ContainerAppsRemediationAgentInput(
                Input: input,
                ToolSignatures: _toolSignatures,
                ThreadId: threadId,
                HelperAgentsInputs: GetHelperAgentInputs()),
            new StartOrchestrationOptions(InstanceId: instanceId));
    }

    public string DeserializeInput(string serializedOrchestrationInput)
    {
        return JsonSerializer.Deserialize<ContainerAppsRemediationAgentInput>(serializedOrchestrationInput).ThrowIfNull().Input;
    }

    private static IReadOnlyList<HelperAgentInput> GetHelperAgentInputs()
    {
        var diagnosticAgentTools = new DiagnosisAgentToolsRegistry();

        diagnosticAgentTools.RegisterReadOnlyPlugin<ContainerAppPluginDefinition>();
        diagnosticAgentTools.RegisterReadOnlyTool<NSGRulePluginDefinition>(x => x.GetNSGRules);
        diagnosticAgentTools.RegisterReadOnlyPlugin<AzureMonitorMetricsPluginDefinition>();
        diagnosticAgentTools.RegisterReadOnlyTool<GraphDBPluginDefinition>(x => x.FindAllNetworkConnectedResources);

        var diagnosticAgentInput = new DiagnosisAgentInput
        {
            ToolSignatures = diagnosticAgentTools.ToolSignatures,
            CustomInstructions = """
            Start with application health investigation. For network issues, prioritize NSG (network security group) analysis first instead.

            **Understand the Container App Configuration:**
            - single revision mode means that we manage a single active revision at a time. When a new revision becomes active, the old one is deactivated
            - multiple means there are more than 1 active revision, and the user manage them. List revisions using list_containerapp_revisions.
            - if there are identities in identitySettings, those are Azure IAM identities. lifecycle tells which container they are assigned to.
            - traffic shows the split of traffic between revisions. If it's single mode, then traffic is always going to the latest ready revision. If it's multiple then traffic goes where set.
            - the target port is the port your application is listening on. You can omit that port and we will auto-discover it.
            - external apps are accessible outside the environment.

            Image pull failures may be due to network connectivity problems or authentication issues.

            Low volume of requests on its own does not indicate an issue.
            """
        };

        return [diagnosticAgentInput];
    }
}
