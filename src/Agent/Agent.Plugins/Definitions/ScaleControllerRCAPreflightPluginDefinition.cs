// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Core.Models.ICM;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Framework;
using Agent.Plugins.Helpers;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions
{

    [AgentToolPlugin(IsFirstPartyOnly = true, Category = ToolCategories.LogQuery, ResourceType = ToolResourceTypes.AppService)]
    public class ScaleControllerRCAPreflightPluginDefinition
    {
        private readonly IKustoPlugin _kustoPlugin;
        private readonly IICMPlugin _icmPlugin;
        private const string DefaultClusterName = "wawscus";
        private const string DefaultDatabaseName = "wawsprod";

        public ScaleControllerRCAPreflightPluginDefinition(IKustoPlugin kustoPlugin, IICMPlugin icmPlugin)
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
            [Description("Kusto database name. wawsprod or legion")] string databaseName,
            [Description("Start time of the query in yyyy-MM-ddTHH:mm:ss.fff format.")] string fromDate,
            [Description("End time of the query in yyyy-MM-ddTHH:mm:ss.fff format.")] string toDate,
            [Description("EventPrimaryStampName (e.g., waws-prod-sy3-099).")] string eventPrimaryStampName,
            [Description("SiteName/application to monitor.")] string siteName)
        {
            var message = FunctionsHelper.ProcessEventPrimaryStampName(eventPrimaryStampName, out bool isValid);
            if (!isValid)
            {
                return Task.FromResult(message);
            }

            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.ScaleControllerPreflight.CheckIfScaleControllerMonitorsApp", clusterName, databaseName,
                new Dictionary<string, string>
                {
                    { "startTime", fromDate },
                    { "endTime", toDate },
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
            [Description("Kusto database name. wawsprod or legion")] string databaseName,
            [Description("Start time of the query in yyyy-MM-ddTHH:mm:ss.fff format.")] string fromDate,
            [Description("End time of the query in yyyy-MM-ddTHH:mm:ss.fff format.")] string toDate,
            [Description("EventPrimaryStampName (e.g., waws-prod-sy3-099).")] string eventPrimaryStampName,
            [Description("SiteName/application to check for errors.")] string siteName,
            [Description("Level (e.g. 4: warning, 3: error.")] int level)
        {
            var message = FunctionsHelper.ProcessEventPrimaryStampName(eventPrimaryStampName, out bool isValid);
            if (!isValid)
            {
                return Task.FromResult(message);
            }
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.ScaleControllerPreflight.GetScaleControllerErrorsForApp", clusterName, databaseName,
                new Dictionary<string, string>
                {
                    { "startTime", fromDate },
                    { "endTime", toDate },
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
        public async Task<string> CheckIfScaleControllerMonitorsTrigger(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name. wawsprod or legion")] string databaseName,
            [Description("Start time of the query in yyyy-MM-ddTHH:mm:ss.fff format.")] string fromDate,
            [Description("End time of the query in yyyy-MM-ddTHH:mm:ss.fff format.")] string toDate,
            [Description("EventPrimaryStampName (e.g., waws-prod-sy3-099).")] string eventPrimaryStampName,
            [Description("SiteName/application.")] string siteName,
            [Description("FunctionName to monitor.")] string functionName)
        {
            var message = FunctionsHelper.ProcessEventPrimaryStampName(eventPrimaryStampName, out bool isValid);
            if (!isValid)
            {
                return message;
            }
            message = FunctionsHelper.ProcessFunctionName(functionName, out isValid);
            if (!isValid)
            {
                return message;
            }
            return await _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.ScaleControllerPreflight.CheckIfScaleControllerMonitorsTrigger", clusterName, databaseName,
                new Dictionary<string, string>
                {
                    { "startTime", fromDate },
                    { "endTime", toDate },
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
            [Description("Kusto database name. wawsprod or legion")] string databaseName,
            [Description("Start time of the query in yyyy-MM-ddTHH:mm:ss.fff format.")] string fromDate,
            [Description("End time of the query in yyyy-MM-ddTHH:mm:ss.fff format.")] string toDate,
            [Description("EventPrimaryStampName (e.g., waws-prod-sy3-099).")] string eventPrimaryStampName,
            [Description("SiteName/application.")] string siteName,
            [Description("TimeBucket for summarization (default: 1m). Examples: 1m, 5m, 1h.")] string summarizationTimeBucket = "1m")
        {
            var message = FunctionsHelper.ProcessEventPrimaryStampName(eventPrimaryStampName, out bool isValid);
            if (!isValid)
            {
                return Task.FromResult(message);
            }
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.ScaleControllerPreflight.CheckScaleControllerVotesToDataService", clusterName, databaseName,
                new Dictionary<string, string>
                {
                    { "startTime", fromDate },
                    { "endTime", toDate },
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
            [Description("Kusto database name. wawsprod or legion")] string databaseName,
            [Description("Start time of the query in yyyy-MM-ddTHH:mm:ss.fff format.")] string fromDate,
            [Description("End time of the query in yyyy-MM-ddTHH:mm:ss.fff format.")] string toDate,
            [Description("EventPrimaryStampName (e.g., waws-prod-sy3-099).")] string eventPrimaryStampName,
            [Description("SiteName/application.")] string siteName,
            [Description("TimeBucket for summarization (default: 5m). Examples: 1m, 5m, 1h.")] string summarizationTimeBucket = "5m")
        {

            // For each occurrence of the error, wrap the string in Task.FromResult to convert it to a Task<string>
            var message = FunctionsHelper.ProcessEventPrimaryStampName(eventPrimaryStampName, out bool isValid);
            if (!isValid)
            {
                return Task.FromResult(message);
            }
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.ScaleControllerPreflight.CheckAssignedWorkersForApp", clusterName, databaseName,
                new Dictionary<string, string>
                {
                    { "startTime", fromDate },
                    { "endTime", toDate },
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
        public async Task<string> CheckFunctionExecutions(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name. wawsprod or legion")] string databaseName,
            [Description("Start time of the query in yyyy-MM-ddTHH:mm:ss.fff format.")] string fromDate,
            [Description("End time of the query in yyyy-MM-ddTHH:mm:ss.fff format.")] string toDate,
            [Description("EventPrimaryStampName (e.g., waws-prod-sy3-099).")] string eventPrimaryStampName,
            [Description("SiteName/application.")] string siteName,
            [Description("FunctionName to monitor.")] string functionName,
            [Description("TimeBucket for summarization (default: 5m). Examples: 1m, 5m, 1h.")] string summarizationTimeBucket = "5m")
        {
            var message = FunctionsHelper.ProcessFunctionName(functionName, out bool isValid);
            if (!isValid)
            {
                return message;
            }
            message = FunctionsHelper.ProcessEventPrimaryStampName(eventPrimaryStampName, out isValid);
            if (!isValid)
            {
                return message;
            }
            return await _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.ScaleControllerPreflight.CheckFunctionExecutions", clusterName, databaseName,
                new Dictionary<string, string>
                {
                    { "startTime", fromDate },
                    { "endTime", toDate },
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
        public async Task<string> CheckProcessingDelaysForFunction(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name. wawsprod or legion")] string databaseName,
            [Description("Start time of the query in yyyy-MM-ddTHH:mm:ss.fff format.")] string fromDate,
            [Description("End time of the query in yyyy-MM-ddTHH:mm:ss.fff format.")] string toDate,
            [Description("EventPrimaryStampName (e.g., waws-prod-sy3-099).")] string eventPrimaryStampName,
            [Description("SiteName/application.")] string siteName,
            [Description("FunctionName to analyze for delays.")] string functionName)
        {
            var message = FunctionsHelper.ProcessFunctionName(functionName, out bool isValid);
            if (!isValid)
            {
                return message;
            }
            message = FunctionsHelper.ProcessEventPrimaryStampName(eventPrimaryStampName, out isValid);
            if (!isValid)
            {
                return message;
            }

            return await _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.ScaleControllerPreflight.CheckProcessingDelaysForFunction", clusterName, databaseName,
                new Dictionary<string, string>
                {
                    { "startTime", fromDate },
                    { "endTime", toDate },
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
    [Description("Kusto database name. wawsprod or legion")] string databaseName,
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
        [AgentTool(ToolMode.Auto)]
        public async Task<string> GetKustoClusterFromEventPrimaryStampName(
            [Description("EventPrimaryStampName (e.g., waws-prod-sy3-099).")] string eventPrimaryStampName)
        {
            var message = FunctionsHelper.ProcessEventPrimaryStampName(eventPrimaryStampName, out bool isValid);
            if (!isValid)
            {
                return message;
            }

            var a = await _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("GetKustoClusterFromEventPrimaryStampName", DefaultClusterName, DefaultDatabaseName,
                new Dictionary<string, string>
                {
                    { "eventPrimaryStampName", eventPrimaryStampName }
                });
            return a;
        }

        [Description(@"""
Retrieves the Kusto cluster name based on the site name by looking up recent analytics events.
Use this tool to determine which Kusto cluster should be used for queries based on the site name.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- EventPrimaryStampName: Event primary stamp name associated with the site.
- KustoCluster: Name of the Kusto cluster associated with the site name.
"""
)]
        [AgentTool(ToolMode.Auto)]
        public Task<string> GetKustoClusterFromSiteName(
            [Description("SiteName/application to look up.")] string siteName)
        {
            var a = _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("GetKustoClusterFromSiteName", DefaultClusterName, DefaultDatabaseName,
                new Dictionary<string, string>
                {
                    { "siteName", siteName }
                });
            return a;
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
            var a =  _icmPlugin.GetParametersFromIncident(incidentId, instruction);
            return a;
        }

        [Description("Get ICM incident details")]
        [AgentTool(ToolMode.Auto)]
        public async Task<Incident> GetIncidentInfoForFunctions(
            [Description("Incident ID")] string incidentId)
        {
            var a = await _icmPlugin.GetIncidentInfo(incidentId);
            return a;
        }
    }
}
