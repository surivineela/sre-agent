// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Core.Attributes;
using Agent.Plugins.Definitions;

namespace Agent.Runtime.AgentTasks.Handlers;

public static class IncidentInvestigationHelper
{
    private static readonly string[] _filterOutTools = [
        "CheckPostgreSQLConnectivity",
        "GetResourceHealth",
        "AnalyzeTableBloat",
        "AnalyzeAutovacuumConfiguration",
        "AnalyzeTableActivity",
        "GetDatabaseOverview",
        "ValidateEnhancedMetricsConfiguration",
        "AnalyzePostgreSQLHealth",
        "GetPostgreSQLMetricsWithGroups", // depends on validate enhanced metrics,
        "GetMetricTimeSeriesElementsForAzureResource", // returns too much raw data,
        // these metrics tools only get current metrics, can't target timestamp
        "GetContainerAppMemoryMetrics",
        "GetContainerAppRequestMetrics",
        "GetContainerAppCpuMetrics"
    ];

    public static bool FilterTools(MethodInfo methodInfo)
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
            methodInfo.DeclaringType == typeof(AgentReasoningControlFlowPluginDefinition) ||
            methodInfo.DeclaringType == typeof(AgentInteractionPluginDefinition))
        {
            return false;
        }

        if (_filterOutTools.Contains(methodInfo.Name))
        {
            return false;
        }

        return true;
    }
}
