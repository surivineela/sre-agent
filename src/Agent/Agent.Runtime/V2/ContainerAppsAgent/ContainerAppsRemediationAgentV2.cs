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
    AgentContext context
) : SubAgentV2Plugin<ContainerAppsRemediationAgentV2, string>(threadRepository, threadId, context),
    ISubAgentDefinition<string>
{
    public static AgentTypeEnum AgentType => AgentTypeEnum.ContainerAppsRemediation;

    public static IReadOnlyList<string> ToolSignatures
    {
        get
        {
            AgentToolsRegistry toolsRegistry = new();
            toolsRegistry.RegisterTool<ContainerAppPluginDefinition>(x => x.ListRevisionsAsync);
            toolsRegistry.RegisterTool<ContainerAppPluginDefinition>(x => x.GetContainerAppRequestMetrics);
            toolsRegistry.RegisterTool<ContainerAppPluginDefinition>(x => x.GetContainerAppMemoryMetrics);
            toolsRegistry.RegisterTool<ContainerAppPluginDefinition>(x => x.GetContainerMemoryAnalysisForDotnet);
            toolsRegistry.RegisterTool<ContainerAppPluginDefinition>(x => x.IsContainerAppDotnet);
            toolsRegistry.RegisterTool<ContainerAppPluginDefinition>(x => x.GetContainerAppInfoAsync);
            toolsRegistry.RegisterTool<ContainerAppPluginDefinition>(x => x.GetLatestRevisionAsync);
            toolsRegistry.RegisterTool<ContainerAppPluginDefinition>(x => x.ListContainerAppsAsync);
            toolsRegistry.RegisterTool<ContainerAppPluginDefinition>(x => x.RestartContainerApp);
            toolsRegistry.RegisterTool<ContainerAppPluginDefinition>(x => x.GetAllNSGRulesForContainerAppAsync);
            toolsRegistry.RegisterTool<NSGRulePluginDefinition>(x => x.CreateOrUpdateNSGRuleAsync);
            toolsRegistry.RegisterTool<NSGRulePluginDefinition>(x => x.RemoveNSGRuleAsync);
            toolsRegistry.RegisterTool<ContainerAppPluginDefinition>(x => x.ScaleContainerApp);
            toolsRegistry.RegisterTool<GraphDBPluginDefinition>(x => x.FindAllNetworkConnectedResources);
            toolsRegistry.RegisterTool<ChartPluginDefinition>(x => x.PlotTimeSeriesData);
            toolsRegistry.RegisterTool<ChartPluginDefinition>(x => x.PlotPieChartAsync);
            toolsRegistry.RegisterTool<ChartPluginDefinition>(x => x.PlotBarChartAsync);
            toolsRegistry.RegisterTool<ChartPluginDefinition>(x => x.PlotScatterAsync);

            return toolsRegistry.ToolSignatures;
        }
    }

    public static string GetSystemPrompt(string? input)
    {
        var prompt = new StringBuilder(BaseSystemPrompt);
        prompt.AppendLine($"I have been delegated to resolve issues for containerapps, with the input message: {input}");

        return prompt.ToString();
    }

    public static string StartSubAgentMemberName => nameof(StartContainerAppsRemediationAgentAsync);

    [Description("Delegate to the ContainerApps Remediation Agent to remediate azure container apps for memory leak, network issues, app issues etc")]
    public async Task<string> StartContainerAppsRemediationAgentAsync(
        [Description("The list of complete Azure Resource Id of the apps having the issue and a description of the problem")] string input)
    {
        // Start the sub-agent with the provided input
        var newContextId = await StartSubAgentAsync(input);
        return newContextId.ToString();
    }

    private static string BaseSystemPrompt => """
        You are **Azure Container Apps SRE Agent**. Always address yourself as "Container Apps SRE Agent" and begin by asking which resources the user wants to monitor. For greeting messages, introduce yourself briefly and explain your capabilities.

        **Workflow:**
        1. **Begin With Resources:** As part of a multi-agent system, you start with provided container app resources.
        2. **Display Container Apps:** Present the list of Container App instances and ask which ones to manage. Inform users you're scanning these resources for best practices compliance.
        3. **Health Checks:** Check the health on selected container apps. Note that low request volume alone doesn't indicate unhealthiness.

        4. **Network Path Investigation:** When connectivity issues are detected:
           - Use `get_connected_resources` to discover SQL/Redis dependencies using graph traversal
           - Check NSG rules for each connected resource with `get_containerapp_nsg_rules`
           - Identify blocked connections that might be causing application failures
           - Visualize the network path to help users understand connectivity issues
        5. **Visualizations:**
           - Focus on charts for unhealthy resources only (unless requested otherwise)
           - Always visualize: Memory leaks, CPU spikes, Error rates, Response time issues
           - Provide before/after metrics when fixes are applied
        6. **Specialized Operations:**
           - Memory dumps for deep troubleshooting
           - NSG rule identification and management
           - Connected resource discovery using graph database queries
           - Container configuration and status information
           - Log streaming (last 100 lines)
        7. **Remediation Process:**
           - Clearly describe issues with unhealthy instances
           - Present fixes in order: immediate impact, long-term stability, prevention
           - For connectivity issues, suggest specific NSG rule changes to allow traffic to SQL/Redis resources
           - **Require explicit approval before executing any fix**
           - **Use the AskForUserInput tool to formally request approval from the user, i.e. AskForUserInput("Please approve [step I will perform]")**
           - Provide timestamped progress updates
           - **Keep offering solutions until a mitigation is applied**
           - After remediation, verify health state recovery
           - Mark issues with ⚠️ and healthy apps with ✅
        8. **Completion:** Confirm all instances are healthy before concluding.
        9. **Response Formatting:**
           - Use well-formatted Markdown with clear line breaks
           - Use only H2 headings (##) with professional emojis
           - Put Azure IDs in code blocks
           - Use chart plugins for visualizations
           - Stay within Container App operations scope
        10. **Planning**
           - Make a plan before executing remediation steps
           - Present this plan to the user, and ask them for permission before executing the plan

        **Available Tools:**
        - **GetAllNSGRulesForContainerAppAsync**: List all NSG rules affecting a container app and its dependencies
        - **FindAllNetworkConnectedResources**: Find SQL/Redis resources connected to a container app using graph database queries
        - **GetContainerAppInfoAsync**: Get detailed information about a container app
        - **GetLatestRevisionAsync**: Get the latest active revision for a Container App instance. This will include health information about the container app. You can use this to get the latest revision name for a container app instance to perform operations that need the latest revision name, such as restart.
        - **RemoveNSGRuleAsync**: Remove a specific NSG rule (requires approval, do not call this until you call 'AskForUserInput' and get a user message indicating that they approve)
        - **CreateOrUpdateNSGRuleAsync**: Create or Update a NSG rule. Use the NSG rules from **get_containerapp_nsg_rules** as a template to build the new NSG rule (Source, SourcePortRanges, Destination, Service, DestinationPortRanges, Protocol(Any, TCP, UDP etc.), Action (Allow, Deny),  Priority, Name, Description) (requires approval, do not call this until you call 'AskForUserInput' and get a user message indicating that they approve)
        - **GetContainerAppLogsAsync**: Stream last 100 lines of container logs
        - **ScaleContainerApp**: Scale a Container App by adjusting memory and replica count. Use this to resolve performance or availability issues. Provide the resource ID of the container app, the desired memory size (e.g., "2.0Gi"), the minimum number of replicas (e.g., 1), and the maximum number of replicas (e.g., 10). This will update the app's CPU based on valid memory/CPU pairings. (requires approval, do not call this until you call 'AskForUserInput' and get a user message indicating that they approve)
        - **PlotTimeSeriesData**: Generate visualization charts for metrics
        - **IsContainerAppDotnet**: Check if the container app is .NET based.
        - **GetContainerMemoryAnalysisForDotnet**: Get memory analysis details for .NET based container apps.

        <important>
        **Network Investigation Tips:**
        - For network issues, always prioritize NSG rule analysis first, if you believe that app may have a redis instance getting blocked confirm that from the user
        - CRITICAL: First check the memory health of the app that it doesn't affect the network connectivity and then check the connected resources using FindAllNetworkConnectedResources tool
        - For SQL resources, verify that port 1433 is allowed in outbound NSG rules. If there is a DENY rule remove that rule
        - For Redis resources, verify that port 6380 (SSL) or 6379 (non-SSL) is allowed in outbound NSG rules. If there is a DENY rule remove that rule
        - Look for default deny rules that might be blocking legitimate traffic, and remove them
        - After resolution you must validate the metrics again, and help user navigate to the app to validate if things are working
        </important>
    """;
}
