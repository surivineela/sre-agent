// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Plugins;
using Agent.Plugins.Definitions;

namespace Agent.Runtime.V2.ContainerAppsAgent;

public class ContainerAppsRemediationAgentV2(
    IThreadRepository threadRepository,
    Guid threadId,
    AgentContext? context
) : SubAgentV2Plugin<ContainerAppsRemediationAgentV2, string>(threadRepository, threadId, context),
    ISubAgentDefinition<string>
{
    static AgentTypeEnum ISubAgentDefinition<string>.AgentType => AgentTypeEnum.ContainerAppsRemediation;

    static IReadOnlyList<string> ISubAgentDefinition<string>.ToolSignatures
    {
        get
        {
            AgentToolsRegistry toolsRegistry = new();
            toolsRegistry.RegisterTool<ContainerAppPluginDefinition>(x => x.ListRevisionsAsync);
            toolsRegistry.RegisterTool<ContainerAppPluginDefinition>(x => x.GetContainerAppRequestMetrics);
            toolsRegistry.RegisterTool<ContainerAppPluginDefinition>(x => x.GetContainerAppMemoryMetrics);
            toolsRegistry.RegisterTool<ContainerAppPluginDefinition>(x => x.GetContainerAppInfoAsync);
            toolsRegistry.RegisterTool<ContainerAppPluginDefinition>(x => x.GetLatestRevisionAsync);
            toolsRegistry.RegisterTool<ContainerAppPluginDefinition>(x => x.ListContainerAppsAsync);
            toolsRegistry.RegisterTool<ContainerAppPluginDefinition>(x => x.RestartContainerApp);
            toolsRegistry.RegisterTool<ContainerAppPluginDefinition>(x => x.GetAllNSGRulesForContainerAppAsync);
            toolsRegistry.RegisterTool<NSGRulePluginDefinition>(x => x.CreateOrUpdateNSGRuleAsync);
            toolsRegistry.RegisterTool<NSGRulePluginDefinition>(x => x.RemoveNSGRuleAsync);
            toolsRegistry.RegisterTool<ContainerAppPluginDefinition>(x => x.ScaleContainerApp);
            toolsRegistry.RegisterTool<GraphDBPluginDefinition>(x => x.FindAllNetworkConnectedResources);
            toolsRegistry.RegisterTool<ChartPluginDefinition>(x => x.PlotTimeSeriesDataAsync);
            toolsRegistry.RegisterTool<ChartPluginDefinition>(x => x.PlotPieChartAsync);
            toolsRegistry.RegisterTool<ChartPluginDefinition>(x => x.PlotBarChartAsync);
            toolsRegistry.RegisterTool<ChartPluginDefinition>(x => x.PlotScatterAsync);

            return toolsRegistry.ToolSignatures;
        }
    }

    static string ISubAgentDefinition<string>.GetSystemPrompt(string? input)
    {
        var promptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "V2", "ContainerAppsAgent", "ContainerAppsAgent.txt");
        var prompt = new StringBuilder(File.ReadAllText(promptPath));
        prompt.AppendLine($"I have been delegated to resolve issues for these applications: {input}");

        return prompt.ToString();
    }

    [Description("Delegate to the ContainerApps Remediation Agent to remediate azure container apps for memory leak, network issues, app issues etc")]
    public override Task StartSubAgentAsync(
        [Description("The list of complete Azure Resource Id of the apps having the issue and a description of the problem")] string input)
    {
        return base.StartSubAgentAsync(input);
    }
}
