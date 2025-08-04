// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Text.Json;
using Agent.Core.Attributes;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Plugins.Definitions;

namespace Agent.Runtime.AgentTasks.Handlers;

public static class IncidentInvestigationHelper
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true };

    private static bool FilterTools(MethodInfo methodInfo)
    {
        if (methodInfo.GetCustomAttribute<WriteActionAttribute>() is not null)
        {
            return false;
        }

        if (methodInfo.GetCustomAttribute<RequiresApprovalAttribute>() is not null)
        {
            return false;
        }

        if (methodInfo.DeclaringType == typeof(UserInteractionPluginDefinition) ||
            methodInfo.DeclaringType == typeof(AgentControlFlowPluginDefinition) ||
            methodInfo.DeclaringType == typeof(AgentInteractionPluginDefinition))
        {
            return false;
        }

        return true;
    }

    public static class GatheringContext
    {
        private const string ToolSelectionContext = """
        Below is a list of all tools and their descriptions that may be used to investigate an incident.
        You will be provided with a description of the incident that the next agent will be investigating.
        You must select the most relevant tools to use based on the incident description, and return a list of tool names.

        The tools you select will be used by the next agent to gather general context about the incident. Focus on tools that help with information retrieval.
        The tools that the next agent will need are tools that will help do the following:

        1. Gather or analyze application logs from the affected resources.
        2. Gather activity logs from the affected resources.
        3. Retrieve recent metrics or metrics trends.
        4. Retrieve resource status.
        5. Retrieve resource configuration.
        6. Get recent changes to the affected resources.


        Azure Activity Logs are different from Application Logs. Activity Logs give information about control plane operations,
        application logs are runtime logs from the application itself.

        Application logs may come from Azure Monitor, Application Insights, or from tools that fetch logs directly from the application.
        """;

        public static string GetToolSelectionInstructions(IToolFactory<AgentContext> toolFactory, List<string>? whitelist = null)
        {
            var availableTools = toolFactory.FetchAvailableToolInfo(FilterTools);
            if (whitelist is not null && whitelist.Count > 0)
            {
                availableTools = availableTools.Where(tool => whitelist.Contains(tool.Name)).ToList();
            }
            var text = JsonSerializer.Serialize(availableTools, JsonSerializerOptions);

            return ToolSelectionInstructions
                .Replace(ToolSelectionContextToken, ToolSelectionContext)
                .Replace(AvailableToolsToken, text);
        }
    }

    public static class HypothesisValidation
    {
        private const string ToolSelectionContext = $$"""
        Below is the incident description, the initial investigation summary, and a list of tools that can be used to validate the hypothesis.
        You will be provided with the hypothesis that the next agent will be attempting to validate or invalidate.
        You must choose the most relevant tools to use based on the incident and the hypothesis, and return a list of tool names.

        The tools you select will be used by the next agent to validate or invalidate the hypothesis. Focus on tools that help with information retrieval and analysis.
        The tools that the next agent will need are tools that will help do the following:

        1. Gather or analyze application logs from the affected resources.
        2. Gather activity logs from the affected resources.
        3. Retrieve recent metrics or metrics trends.
        4. Retrieve resource status.
        5. Retrieve resource configuration.
        6. Get recent changes to the affected resources.
        7. Find connected resources (e.g. webapps that connect to external database)

        Azure Activity Logs are different from Application Logs. Activity Logs give information about control plane operations,
        application logs are runtime logs from the application itself.

        Application logs may come from Azure Monitor, Application Insights, or from tools that fetch logs directly from the application.

        # Incident Description
        <incidentDescription>
        {{IncidentDescriptionToken}}
        </incidentDescription>

        # Initial Investigation Summary
        <initialSummary>
        {{InitialInvestigationSummaryToken}}
        </initialSummary>
        """;

        private const string IncidentDescriptionToken = "{incidentDescription}";
        private const string InitialInvestigationSummaryToken = "{initialInvestigationSummary}";

        public static string GetToolSelectionInstructions(
            IToolFactory<AgentContext> toolFactory,
            string incidentDescription,
            string initialInvestigationSummary,
            List<string>? whitelist = null)
        {
            var availableTools = toolFactory.FetchAvailableToolInfo(FilterTools);
            if (whitelist is not null && whitelist.Count > 0)
            {
                availableTools = availableTools.Where(tool => whitelist.Contains(tool.Name)).ToList();
            }
            var text = JsonSerializer.Serialize(availableTools, JsonSerializerOptions);

            return ToolSelectionInstructions
                .Replace(ToolSelectionContextToken, ToolSelectionContext)
                .Replace(AvailableToolsToken, text)
                .Replace(IncidentDescriptionToken, incidentDescription)
                .Replace(InitialInvestigationSummaryToken, initialInvestigationSummary);
        }
    }

    private const string AvailableToolsToken = "{availableTools}";
    private const string ToolSelectionContextToken = "{toolSelectionContext}";

    private const string ToolSelectionInstructions = $$"""
        # Instructions

        You are a helpful agent that can select the most relevant tools to use for the given task. You should consider the type of resource being mentioned,
        and the type of information you are trying to gather.

        To help with the task completion, should should also return tools that help with resource discovery.

        {{ToolSelectionContextToken}}

        Return enough tools for the next agent to perform its task. You should return 4-6 tools.

        # Example

        ## List of all available tools:
        [
            {
                "name": "GetAppConsoleLogs",
                "description": "This function attempts to retrieve error messages in the console logs and platform logs from a user's particular app",
                "parameters": [
                    "resourceId"
                ]
            },
            {
                "name": "PerformDeploymentSwapForApp",
                "description": "Performs a Deployment Swap for the specified app.",
                "parameters": [
                    "resourceId"
                ]
            },
            {
                "name": "GetDeploymentActivity",
                "description": "Gets Deployment Activities on the specified app",
                "parameters": [
                    "resourceId"
                ]
            },
            {
                "name": "GetContainerAppRequestMetrics",
                "description": "Start a background operation to get the total request count metrics of a specific Container App instance at per minute granularity for the past 30 minutes, Container App is healthy if all data points are at least 99.9 availability.",
                "parameters": [
                    "resourceId"
                ]
            },
            {
                "name": "GetContainerAppMemoryMetrics",
                "description": "Start a background operation to get the average memory usage of a specific Container App instance at per minute granularity for the past 30 minutes, Container App is healthy if over half of the data points is less than 20% memory utilization.",
                "parameters": [
                    "resourceId"
                ]
            },
            {
                "name": "GetWebAppCpuMetrics",
                "description": "Get the average CPU utilization metrics of a specific WebApp instance at per minute granularity for the past 30 minutes, WebApp is healthy if over half of the data points is less than 80% CPU utilization, zero metric value doesn't indicate the app is unhealthy",
                "parameters": [
                    "resourceId"
                ]
            }
        ]

        ## Input incident description:
        'The webapp '/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/my-rg/providers/Microsoft.Web/sites/my-webapp' is down.'

        ## Output:
        [
            "GetAppConsoleLogs",
            "GetDeploymentActivity",
            "GetWebAppCpuMetrics"
        ]

        ## Explanation:
        The incident description mentions a webapp that is down. The tools that are most relevant to this incident are:
        - GetAppConsoleLogs: to retrieve error messages in the console logs and platform logs from the affected app
        - GetDeploymentActivity: to retrieve deployment activities on the affected app
        - GetWebAppCpuMetrics: to retrieve CPU utilization metrics of the affected app

        These tools are relevant because they help with gathering information and target the correct Azure resource type.

        The tools that are less relevant to this incident are:
        - GetContainerAppRequestMetrics: to retrieve request count metrics of a specific Container App instance
        - GetContainerAppMemoryMetrics: to retrieve memory usage metrics of a specific Container App instance
        - PerformDeploymentSwapForApp: to perform a deployment swap for the affected app

        GetContainerAppRequestMetrics and GetContainerAppMemoryMetrics are not relevant because they are for the wrong resource type. The incident is about a webapp, not a container app.
        PerformDeploymentSwapForApp is not relevant because it is not a tool that helps with gathering information about the incident.

        The available tools go below:
        <availableTools>
        {{AvailableToolsToken}}
        </availableTools>
        """;
}
