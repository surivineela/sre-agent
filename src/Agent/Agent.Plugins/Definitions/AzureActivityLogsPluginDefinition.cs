// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Plugins.Interface;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(Category = ToolCategories.KnowledgeBase)]
    public class AzureActivityLogsPluginDefinition : ContextToolTarget<AgentContext>
    {
        public IAzureActivityLogsPlugin _plugin { get; }

        public AzureActivityLogsPluginDefinition(IAzureActivityLogsPlugin azureActivityLogsPlugin)
        {
            _plugin = azureActivityLogsPlugin;
        }

        [KernelFunction("GetActivityLogsSummary")]
        [Description("Retrieves and analyzes Azure Activity Logs for a resource and its connected components. " +
            "This function is valuable when you need to: 1) Review recent changes made to a resource and its dependencies, " +
            "2) Investigate who made specific configuration changes, " +
            "3) Understand patterns of administrative activity, or " +
            "4) Detect potentially unauthorized or unusual operations. " +
            "The output is a natural language summary highlighting key activities, patterns, and potential concerns.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> GetActivityLogsSummary(
            [Description("Azure Resource Id of the resource to analyze. Should begin with /subscriptions/... Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp")] string resourceId,
            [Description("Number of hours of activity logs to retrieve and analyze. Default is 24 hours.")] int hoursBack = 24)
        {
            return await _plugin.FetchAndSummarizeActivityLogs(resourceId, hoursBack, Context?.ThreadId);
        }

        [KernelFunction("AnalyzeDeploymentFailures")]
        [Description("Analyzes Azure deployment failures and provides detailed error information for troubleshooting. " +
            "This function is useful when you need to: " +
            "1) Investigate deployment failures and their root causes, " +
            "2) Get detailed error information for failed Azure resource deployments, " +
            "3) Understand deployment issues for troubleshooting purposes, or " +
            "4) Analyze patterns in deployment failures over time. " +
            "The output provides comprehensive analysis of deployment failures with actionable insights.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> AnalyzeDeploymentFailures(
            [Description("Azure Resource Id of the resource to analyze deployment failures for. Should begin with /subscriptions/... Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp")] string resourceId,
            [Description("Number of hours to look back for deployment failures. Default is 24 hours.")] int hoursBack = 24)
        {
            return await _plugin.AnalyzeDeploymentFailures(resourceId, hoursBack, Context?.ThreadId);
        }

        [KernelFunction("GetChangeHistory")]
        [Description("Retrieves detailed change history for a specific activity log entry using correlation ID. " +
            "This function is useful when you need to: " +
            "1) Get comprehensive details about what changes were made during a specific operation, " +
            "2) Understand the complete timeline and context of related operations, " +
            "3) Analyze the impact and scope of changes across multiple resources, " +
            "4) Investigate deployment parameters, configuration changes, or resource modifications, or " +
            "5) Get before/after details when available from Azure deployment history. " +
            "The output provides detailed analysis including change summary, operation timeline, who made changes, and impact analysis. " +
            "Use this when users want to drill down into specific activity log entries for more details.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> GetChangeHistory(
            [Description("Correlation ID from the activity log entry to get detailed change history for. This is the unique identifier that links related operations together.")] string correlationId,
            [Description("Azure Resource Id of the resource that was changed. Should begin with /subscriptions/... Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp")] string resourceId)
        {
            return await _plugin.GetChangeHistory(correlationId, resourceId, Context?.ThreadId);
        }

        [KernelFunction("ShowChangeDiffViewer")]
        [Description("Displays a visual change diff viewer showing detailed property-level changes between before and after states. " +
            "Important: Needs Correlation Id from the particular Activity Log as an Input. This function is useful when you need to: " +
            "1) Visualize exactly what properties were added, removed, or modified in a resource, " +
            "2) Show side-by-side comparison of before and after states with syntax highlighting, " +
            "3) Present complex configuration changes in an easy-to-understand visual format, " +
            "4) Help users quickly identify specific changes that may have caused issues, or " +
            "5) Provide interactive exploration of nested property changes in Azure resources. " +
            "The output displays an interactive diff viewer with color-coded additions (green), deletions (red), and modifications (yellow). " +
            "Use this when users want a visual representation of changes rather than text-based analysis.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> ShowChangeDiffViewer(
            [Description("MUST BE Correlation ID from the **activity log** entry to show change diff for. This is the unique identifier guid that links related operations together. You should use this value as found from the activity log")] string correlationId,
            [Description("Azure Resource Id of the resource that was changed. Should begin with /subscriptions/... Example: /subscriptions/123/resourcegroups/myapp/providers/microsoft.web/sites/mywebapp")] string resourceId,
            [Description("Title to display in the diff viewer header. Should be descriptive of what changes are being shown.")] string title,
            [Description("Description of the changes being displayed. This helps provide context for what the user is viewing.")] string description)
        {
            return await _plugin.ShowChangeDiffViewer(correlationId, resourceId, title, description, Context?.ThreadId);
        }
    }
}
