// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions
{

    [AgentToolPlugin(IsFirstPartyOnly = true, Category = ToolCategories.LogQuery, ResourceType = ToolResourceTypes.AppService)]
    public class ScaleControllerRCAPreflightPluginDefinition
    {
        private readonly IKustoPluginChat _kustoPlugin;
        private readonly IICMPlugin _icmPlugin;
        private const string DefaultClusterName = "wawscus";
        private const string DefaultDatabaseName = "wawsprod";

        public ScaleControllerRCAPreflightPluginDefinition(IKustoPluginChat kustoPlugin, IICMPlugin icmPlugin)
        {
            _kustoPlugin = kustoPlugin;
            _icmPlugin = icmPlugin;
        }

        [Description(@"""
Checks if the Scale Controller is monitoring the specified application by looking for aggregation logs.
Use this tool to verify that Scale Controller is actively monitoring an application.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- PreciseTimeStamp: Time when monitoring activity was recorded.
- count_: Count of monitoring events in each time bucket.
"""
)]
        [AgentTool(ToolMode.Auto)]
        public Task<string> CheckIfScaleControllerMonitorsApp(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("EventPrimaryStampName (e.g., waws-prod-sy3-099).")] string eventPrimaryStampName,
            [Description("SiteName/application to monitor.")] string siteName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.ScaleControllerPreflight.CheckIfScaleControllerMonitorsApp", clusterName, databaseName,
                new Dictionary<string, string>
                {
                    { "startTime", fromDate.ToString() },
                    { "endTime", toDate.ToString() },
                    { "eventPrimaryStampName", eventPrimaryStampName },
                    { "siteName", siteName }
                });
        }

        [Description(@"""
Retrieves Scale Controller errors for the specified application during the given time range.
Use this tool to identify error messages and exceptions that may be affecting application scaling.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- PreciseTimeStamp: Time when the error occurred.
- RoleInstance: Instance that generated the error.
- Message: Error message.
- Exception: Exception details (if any).
- Version: Scale Controller version.
- Level: Log level (errors have Level < 4).
"""
)]
        [AgentTool(ToolMode.Auto)]
        public Task<string> GetScaleControllerErrorsForApp(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("EventPrimaryStampName (e.g., waws-prod-sy3-099).")] string eventPrimaryStampName,
            [Description("SiteName/application to check for errors.")] string siteName,
            [Description("Level (e.g. 4: warning, 3: error.")] int level)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.ScaleControllerPreflight.GetScaleControllerErrorsForApp", clusterName, databaseName,
                new Dictionary<string, string>
                {
                    { "startTime", fromDate.ToString() },
                    { "endTime", toDate.ToString() },
                    { "eventPrimaryStampName", eventPrimaryStampName },
                    { "siteName", siteName },
                    { "level", (level == 0 || level > 4) ? "4" : level.ToString() }
                });
        }

        [Description(@"""
Checks if the Scale Controller is monitoring the specified trigger for an application.
Use this tool to verify that Scale Controller is tracking a specific function trigger.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- PreciseTimeStamp: Time when trigger monitoring was recorded.
- Message: Scale Controller message containing trigger information.
"""
)]
        [AgentTool(ToolMode.Auto)]
        public Task<string> CheckIfScaleControllerMonitorsTrigger(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("EventPrimaryStampName (e.g., waws-prod-sy3-099).")] string eventPrimaryStampName,
            [Description("SiteName/application.")] string siteName,
            [Description("FunctionName to monitor.")] string functionName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.ScaleControllerPreflight.CheckIfScaleControllerMonitorsTrigger", clusterName, databaseName,
                new Dictionary<string, string>
                {
                    { "startTime", fromDate.ToString() },
                    { "endTime", toDate.ToString() },
                    { "eventPrimaryStampName", eventPrimaryStampName },
                    { "siteName", siteName },
                    { "functionName", functionName }
                });
        }

        [Description(@"""
Checks if Scale Controller is sending votes to the Data Service for worker allocation.
Use this tool to verify that Scale Controller is requesting worker instances for the application.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- PreciseTimeStamp: Time when the vote was sent.
- max_Count: Maximum worker count requested in that time bucket.
- SiteName: Name of the site/application.
"""
)]
        [AgentTool(ToolMode.Auto)]
        public Task<string> CheckScaleControllerVotesToDataService(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("EventPrimaryStampName (e.g., waws-prod-sy3-099).")] string eventPrimaryStampName,
            [Description("SiteName/application.")] string siteName,
            [Description("TimeBucket for summarization (default: 1m). Examples: 1m, 5m, 1h.")] string summarizationTimeBucket = "1m")
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.ScaleControllerPreflight.CheckScaleControllerVotesToDataService", clusterName, databaseName,
                new Dictionary<string, string>
                {
                    { "startTime", fromDate.ToString() },
                    { "endTime", toDate.ToString() },
                    { "eventPrimaryStampName", eventPrimaryStampName },
                    { "siteName", siteName },
                    { "summarizationTimeBucket", summarizationTimeBucket }
                });
        }

        [Description(@"""
Checks if the application has assigned workers during the specified time interval.
Use this tool to verify that worker instances are actually allocated to the application.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- PreciseTimeStamp: Time bucket.
- Count: Number of distinct workers assigned to the application.
"""
)]
        [AgentTool(ToolMode.Auto)]
        public Task<string> CheckAssignedWorkersForApp(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("EventPrimaryStampName (e.g., waws-prod-sy3-099).")] string eventPrimaryStampName,
            [Description("SiteName/application.")] string siteName,
            [Description("TimeBucket for summarization (default: 5m). Examples: 1m, 5m, 1h.")] string summarizationTimeBucket = "5m")
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.ScaleControllerPreflight.CheckAssignedWorkersForApp", clusterName, databaseName,
                new Dictionary<string, string>
                {
                    { "startTime", fromDate.ToString() },
                    { "endTime", toDate.ToString() },
                    { "eventPrimaryStampName", eventPrimaryStampName },
                    { "siteName", siteName },
                    { "summarizationTimeBucket", summarizationTimeBucket }
                });
        }

        [Description(@"""
Checks for function executions of a specific trigger/function.
Use this tool to verify that the function is actually being executed.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- PreciseTimeStamp: Time bucket.
- Count: Number of function executions in that time bucket.
"""
)]
        [AgentTool(ToolMode.Auto)]
        public Task<string> CheckFunctionExecutions(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("EventPrimaryStampName (e.g., waws-prod-sy3-099).")] string eventPrimaryStampName,
            [Description("SiteName/application.")] string siteName,
            [Description("FunctionName to monitor.")] string functionName,
            [Description("TimeBucket for summarization (default: 5m). Examples: 1m, 5m, 1h.")] string summarizationTimeBucket = "5m")
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.ScaleControllerPreflight.CheckFunctionExecutions", clusterName, databaseName,
                new Dictionary<string, string>
                {
                    { "startTime", fromDate.ToString() },
                    { "endTime", toDate.ToString() },
                    { "eventPrimaryStampName", eventPrimaryStampName },
                    { "siteName", siteName },
                    { "functionName", functionName },
                    { "summarizationTimeBucket", summarizationTimeBucket }
                });
        }

        [Description(@"""
Checks for processing delays in function execution by analyzing trigger details.
Use this tool to detect delays between message enqueue time and processing time.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- PreciseTimeStamp: Time when the function was processed.
- EnqueueTimeUtc: Time when the message was originally enqueued.
"""
)]
        [AgentTool(ToolMode.Auto)]
        public Task<string> CheckProcessingDelaysForFunction(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("EventPrimaryStampName (e.g., waws-prod-sy3-099).")] string eventPrimaryStampName,
            [Description("SiteName/application.")] string siteName,
            [Description("FunctionName to analyze for delays.")] string functionName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.ScaleControllerPreflight.CheckProcessingDelaysForFunction", clusterName, databaseName,
                new Dictionary<string, string>
                {
                    { "startTime", fromDate.ToString() },
                    { "endTime", toDate.ToString() },
                    { "stampName", eventPrimaryStampName },
                    { "siteName", siteName },
                    { "functionName", functionName }
                });
        }



        [Description(@"""
Retrieves the SyncTriggers payload from SiteName.
Use this tool to determine which Kusto cluster should be used for queries based on the site name.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- Triggers: SyncTriggers payload containing information about triggers for the specified site.
- TriggersLastModifiedTime: Last modified time of the triggers.
"""
)]
        [AgentTool(ToolMode.Auto)]
        public Task<string> GetSyncTriggersPayload(
    [Description("Kusto cluster name.")] string clusterName,
    [Description("Kusto database name.")] string databaseName,
    [Description("Days ago for fetching the payload (default: 1d). Examples: 1d, 3d, 10d.")] string daysAgo,
    [Description("SiteName/application to check for errors.")] string siteName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.GetSyncTriggersFromSiteName", clusterName, databaseName,
                new Dictionary<string, string>
                {
                    { "daysAgo", daysAgo },
                    { "siteName", siteName }
                });
        }

        [Description(@"""
Retrieves the Kusto cluster name based on the event primary stamp name.
Use this tool to determine which Kusto cluster should be used for queries based on the stamp name.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- KustoCluster: Name of the Kusto cluster associated with the event primary stamp name.
"""
)]
        public Task<string> GetKustoClusterFromEventPrimaryStampName(
            [Description("EventPrimaryStampName (e.g., waws-prod-sy3-099).")] string eventPrimaryStampName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("GetKustoClusterFromEventPrimaryStampName", DefaultClusterName, DefaultDatabaseName,
                new Dictionary<string, string>
                {
                    { "eventPrimaryStampName", eventPrimaryStampName }
                });
        }

        [Description(@"""
Retrieves the Kusto cluster name based on the site name by looking up recent analytics events.
Use this tool to determine which Kusto cluster should be used for queries based on the site name.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- EventPrimaryStampName: Event primary stamp name associated with the site.
- KustoCluster: Name of the Kusto cluster associated with the site name.
"""
)]
        public Task<string> GetKustoClusterFromSiteName(
            [Description("SiteName/application to look up.")] string siteName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("GetKustoClusterFromSiteName", DefaultClusterName, DefaultDatabaseName,
                new Dictionary<string, string>
                {
                    { "siteName", siteName }
                });
        }

        [Description(@"""
Extracts RCA investigation parameters from an ICM incident.
Use this tool to automatically extract key parameters like start time, end time, site name, stamp name, and function name from ICM incident details.
This helps streamline the RCA investigation process by parsing incident information.
Output: Returns JSON containing extracted parameters such as incidentId, title, startTime, endTime, siteName, eventPrimaryStampName, functionName, and additionalParameters.
"""
)]
        [AgentTool(ToolMode.Auto)]
        public Task<string> ExtractRCAParametersFromICMIncident(
            [Description("ICM Incident ID to extract parameters from.")] string incidentId,
            [Description("Specific instruction for parameter extraction. Example: 'Extract startTime, endTime, siteName, eventPrimaryStampName, and functionName.'")] string instruction)
        {
            return _icmPlugin.GetParametersFromIncident(incidentId, instruction);
        }
    }
}
