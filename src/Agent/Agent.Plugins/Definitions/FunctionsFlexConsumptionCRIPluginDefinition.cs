// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Agent.Plugins.Definitions
{
    using System.ComponentModel;
    using Agent.Core.Models;
    using Agent.Plugins.Interface;

    [AgentToolPlugin(IsFirstPartyOnly = true,Category = ToolCategories.Diagnostics, ResourceType = ToolResourceTypes.FunctionApp)]
    public class FunctionsFlexConsumptionCRIPluginDefinition
    {
        private readonly IKustoPluginChat _kustoPlugin;

        public FunctionsFlexConsumptionCRIPluginDefinition(IKustoPluginChat kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

        [Description("Gets HTTP status codes distribution during impact duration for troubleshooting Function App availability issues. Use this to understand the scope and types of HTTP errors occurring.")]
        public Task<string> GetHttpStatusCodesForImpactDuration(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Primary stamp name (e.g., 'waws-prod-mwh-089')")] string eventPrimaryStamp,
            [Description("Start time of the impact period in ISO format (e.g., '2025-06-09T03:40:00Z')")] string startTime,
            [Description("End time of the impact period in ISO format (e.g., '2025-06-12T20:24:00Z')")] string endTime,
            [Description("Site names to investigate (e.g., 'd-wus2-streamsetup-func')")] string siteNames)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("AzureFunctions.FlexConsumption.GetHttpStatusCodesForImpactDuration", clusterName, databaseName,
                new Dictionary<string, string> {
                    { "eventPrimaryStamp", eventPrimaryStamp },
                    { "startTime", startTime },
                    { "endTime", endTime },
                    { "siteNames", siteNames }
                });
        }

        [Description("Gets failed HTTP requests for a specific status code to identify problematic requests during impact. Use this after analyzing status code distribution to focus on specific error types.")]
        public Task<string> GetFailedHttpRequests(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Primary stamp name (e.g., 'waws-prod-mwh-089')")] string eventPrimaryStamp,
            [Description("Start time of the impact period in ISO format")] string startTime,
            [Description("End time of the impact period in ISO format")] string endTime,
            [Description("Site names to investigate")] string siteNames,
            [Description("Status code to filter on (e.g., 503, 500)")] string statusCodeImpacted)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("AzureFunctions.FlexConsumption.GetFailedHttpRequests", clusterName, databaseName,
                new Dictionary<string, string> {
                    { "eventPrimaryStamp", eventPrimaryStamp },
                    { "startTime", startTime },
                    { "endTime", endTime },
                    { "siteNames", siteNames },
                    { "statusCodeImpacted", statusCodeImpacted }
                });
        }

        [Description("Gets activity IDs of failed HTTP requests for deeper investigation. Use these activity IDs to trace requests through platform logs and understand root causes.")]
        public Task<string> GetActivityIdsOfFailedHttpRequests(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Primary stamp name")] string eventPrimaryStamp,
            [Description("Start time of the impact period in ISO format")] string startTime,
            [Description("End time of the impact period in ISO format")] string endTime,
            [Description("Site names to investigate")] string siteNames,
            [Description("Status code to filter on")] string statusCodeImpacted)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("AzureFunctions.FlexConsumption.GetActivityIdsOfFailedHttpRequests", clusterName, databaseName,
                new Dictionary<string, string> {
                    { "eventPrimaryStamp", eventPrimaryStamp },
                    { "startTime", startTime },
                    { "endTime", endTime },
                    { "siteNames", siteNames },
                    { "statusCodeImpacted", statusCodeImpacted }
                });
        }

        [Description("Checks for specialization failures during impact duration. Specialization is the process of preparing a worker for a specific function app. Use this to identify if apps are failing to specialize properly.")]
        public Task<string> CheckSpecializationFailuresDuringImpact(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Array of antares stamps (e.g., ['waws-prod-mwh-089'])")] string antaresStamps,
            [Description("App name for context")] string appName,
            [Description("Start time in ISO format")] string startTime,
            [Description("End time in ISO format")] string endTime,
            [Description("Site names to check (comma-separated if multiple)")] string siteNames,
            [Description("Number of rows to return")] string numRows,
            [Description("Private stamp name if applicable")] string privateStampName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("AzureFunctions.FlexConsumption.CheckSpecializationFailuresDuringImpact", clusterName, databaseName,
                new Dictionary<string, string> {
                    { "antaresStamps", antaresStamps },
                    { "appName", appName },
                    { "startTime", startTime },
                    { "endTime", endTime },
                    { "siteNames", siteNames },
                    { "numRows", numRows },
                    { "privateStampName", privateStampName }
                });
        }

        [Description("Gets activity ID of a failed specialization request for detailed tracing. Use this to get a specific failed specialization to trace through logs.")]
        public Task<string> GetActivityIdOfFailedSpecialization(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Array of antares stamps")] string antaresStamps,
            [Description("App name")] string appName,
            [Description("Start time in ISO format")] string startTime,
            [Description("End time in ISO format")] string endTime,
            [Description("Site names to check")] string siteNames,
            [Description("Status code to check for (e.g., 403, 500)")] string statusCodeToCheck)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("AzureFunctions.FlexConsumption.GetActivityIdOfFailedSpecialization", clusterName, databaseName,
                new Dictionary<string, string> {
                    { "antaresStamps", antaresStamps },
                    { "appName", appName },
                    { "startTime", startTime },
                    { "endTime", endTime },
                    { "siteNames", siteNames },
                    { "statusCodeToCheck", statusCodeToCheck }
                });
        }

        [Description("Traces an activity ID through Functions Platform Logs to understand the complete request flow and identify where failures occurred.")]
        public Task<string> TraceActivityIdThroughFunctionsPlatformLogs(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Array of antares stamps")] string antaresStamps,
            [Description("App name")] string appName,
            [Description("Start time in ISO format")] string startTime,
            [Description("End time in ISO format")] string endTime,
            [Description("Site names to trace")] string siteNames,
            [Description("Activity ID to trace")] string activityId)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("AzureFunctions.FlexConsumption.TraceActivityIdThroughFunctionsPlatformLogs", clusterName, databaseName,
                new Dictionary<string, string> {
                    { "antaresStamps", antaresStamps },
                    { "appName", appName },
                    { "startTime", startTime },
                    { "endTime", endTime },
                    { "siteNames", siteNames },
                    { "activityId", activityId }
                });
        }

        [Description("Checks for allocation failures due to capacity issues. This identifies when pod allocation fails because there's no available capacity in the cluster.")]
        public Task<string> CheckAllocationFailuresDueToCapacity(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Array of antares stamps")] string antaresStamps,
            [Description("App name")] string appName,
            [Description("Start time in ISO format")] string startTime,
            [Description("End time in ISO format")] string endTime,
            [Description("Site names to check")] string siteNames,
            [Description("Number of rows to return")] string numRows,
            [Description("Private stamp name if applicable")] string privateStampName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("AzureFunctions.FlexConsumption.CheckAllocationFailuresDueToCapacity", clusterName, databaseName,
                new Dictionary<string, string> {
                    { "antaresStamps", antaresStamps },
                    { "appName", appName },
                    { "startTime", startTime },
                    { "endTime", endTime },
                    { "siteNames", siteNames },
                    { "numRows", numRows },
                    { "privateStampName", privateStampName }
                });
        }

        [Description("Checks for allocation failures during impact duration. Allocation is the process of assigning compute resources to function apps.")]
        public Task<string> CheckAllocationFailuresDuringImpact(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Array of antares stamps")] string antaresStamps,
            [Description("App name")] string appName,
            [Description("Start time in ISO format")] string startTime,
            [Description("End time in ISO format")] string endTime,
            [Description("Site names to check")] string siteNames,
            [Description("Number of rows to return")] string numRows,
            [Description("Private stamp name if applicable")] string privateStampName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("AzureFunctions.FlexConsumption.CheckAllocationFailuresDuringImpact", clusterName, databaseName,
                new Dictionary<string, string> {
                    { "antaresStamps", antaresStamps },
                    { "appName", appName },
                    { "startTime", startTime },
                    { "endTime", endTime },
                    { "siteNames", siteNames },
                    { "numRows", numRows },
                    { "privateStampName", privateStampName }
                });
        }

        [Description("Gets activity ID of a failed allocation request for detailed investigation.")]
        public Task<string> GetActivityIdOfFailedAllocationRequest(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Array of antares stamps")] string antaresStamps,
            [Description("App name")] string appName,
            [Description("Start time in ISO format")] string startTime,
            [Description("End time in ISO format")] string endTime,
            [Description("Site names to check")] string siteNames,
            [Description("Failed status code to look for (e.g., 500)")] string failedStatusCode)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("AzureFunctions.FlexConsumption.GetActivityIdOfFailedAllocationRequest", clusterName, databaseName,
                new Dictionary<string, string> {
                    { "antaresStamps", antaresStamps },
                    { "appName", appName },
                    { "startTime", startTime },
                    { "endTime", endTime },
                    { "siteNames", siteNames },
                    { "failedStatusCode", failedStatusCode }
                });
        }

        [Description("Gets failure details for a specific activity ID to understand what went wrong with a particular request.")]
        public Task<string> GetFailureDetailsForActivityId(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Array of antares stamps")] string antaresStamps,
            [Description("App name")] string appName,
            [Description("Start time in ISO format")] string startTime,
            [Description("End time in ISO format")] string endTime,
            [Description("Activity ID to investigate")] string activityId)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("AzureFunctions.FlexConsumption.GetFailureDetailsForActivityId", clusterName, databaseName,
                new Dictionary<string, string> {
                    { "antaresStamps", antaresStamps },
                    { "appName", appName },
                    { "startTime", startTime },
                    { "endTime", endTime },
                    { "activityId", activityId }
                });
        }

        [Description("Checks if a site is currently under penalty. Sites are penalized when they exceed certain resource limits or exhibit problematic behavior.")]
        public Task<string> CheckIfSiteIsUnderPenalty(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Array of antares stamps")] string antaresStamps,
            [Description("App name")] string appName,
            [Description("Start time in ISO format")] string startTime,
            [Description("End time in ISO format")] string endTime,
            [Description("Site names to check")] string siteNames,
            [Description("Number of rows to return")] string numRows,
            [Description("Private stamp name if applicable")] string privateStampName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("AzureFunctions.FlexConsumption.CheckIfSiteIsUnderPenalty", clusterName, databaseName,
                new Dictionary<string, string> {
                    { "antaresStamps", antaresStamps },
                    { "appName", appName },
                    { "startTime", startTime },
                    { "endTime", endTime },
                    { "siteNames", siteNames },
                    { "numRows", numRows },
                    { "privateStampName", privateStampName }
                });
        }

        [Description("Checks if a site is throttled due to subscription core throttling. This happens when the subscription reaches its regional core limit.")]
        public Task<string> CheckIfSiteIsThrottledDueToSubCoreThrottling(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Array of antares stamps")] string antaresStamps,
            [Description("App name")] string appName,
            [Description("Start time in ISO format")] string startTime,
            [Description("End time in ISO format")] string endTime,
            [Description("Site names to check")] string siteNames,
            [Description("Number of rows to return")] string numRows,
            [Description("Private stamp name if applicable")] string privateStampName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("AzureFunctions.FlexConsumption.CheckIfSiteIsThrottledDueToSubCoreThrottling", clusterName, databaseName,
                new Dictionary<string, string> {
                    { "antaresStamps", antaresStamps },
                    { "appName", appName },
                    { "startTime", startTime },
                    { "endTime", endTime },
                    { "siteNames", siteNames },
                    { "numRows", numRows },
                    { "privateStampName", privateStampName }
                });
        }

        [Description("Gets Scale Controller logs to understand scaling decisions and issues. Scale Controller manages the scaling behavior of Function Apps.")]
        public Task<string> GetScaleControllerLogs(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Site name to get logs for")] string siteName,
            [Description("Event primary stamp name")] string eventPrimaryStampName,
            [Description("Start time in ISO format")] string startTime,
            [Description("End time in ISO format")] string endTime)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("AzureFunctions.FlexConsumption.GetScaleControllerLogs", clusterName, databaseName,
                new Dictionary<string, string> {
                    { "siteName", siteName },
                    { "eventPrimaryStampName", eventPrimaryStampName },
                    { "startTime", startTime },
                    { "endTime", endTime }
                });
        }

        [Description("Gets Function Host logs to investigate runtime issues and application-level problems. Function Host is the runtime that executes your functions.")]
        public Task<string> GetFunctionHostLogs(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("App name to get logs for")] string appName,
            [Description("Event primary stamp name")] string eventPrimaryStampName,
            [Description("Start time in ISO format")] string startTime,
            [Description("End time in ISO format")] string endTime)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("AzureFunctions.FlexConsumption.GetFunctionHostLogs", clusterName, databaseName,
                new Dictionary<string, string> {
                    { "appName", appName },
                    { "eventPrimaryStampName", eventPrimaryStampName },
                    { "startTime", startTime },
                    { "endTime", endTime }
                });
        }

        [Description("Checks for HTTP triggers where worker was not assigned (503.65 errors). This specific error indicates that no worker instance was available to process the HTTP request.")]
        public Task<string> CheckWorkerNotAssignedForHttpTriggers(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Primary stamp name")] string eventPrimaryStamp,
            [Description("Start time in ISO format")] string startTime,
            [Description("End time in ISO format")] string endTime,
            [Description("Site names to check")] string siteNames,
            [Description("Status code impacted (usually 503)")] string statusCodeImpacted)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("AzureFunctions.FlexConsumption.CheckWorkerNotAssignedForHttpTriggers", clusterName, databaseName,
                new Dictionary<string, string> {
                    { "eventPrimaryStamp", eventPrimaryStamp },
                    { "startTime", startTime },
                    { "endTime", endTime },
                    { "siteNames", siteNames },
                    { "statusCodeImpacted", statusCodeImpacted }
                });
        }

        [Description("Checks if worker assignment is throttled for the site. This identifies when worker assignment is being throttled due to various reasons like resource constraints or policy limits.")]
        public Task<string> CheckIfWorkerAssignmentIsThrottledForTheSite(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Array of antares stamps")] string antaresStamps,
            [Description("App name")] string appName,
            [Description("Start time in ISO format")] string startTime,
            [Description("End time in ISO format")] string endTime,
            [Description("Site names to check")] string siteNames,
            [Description("Number of rows to return")] string numRows,
            [Description("Private stamp name if applicable")] string privateStampName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("AzureFunctions.FlexConsumption.CheckIfWorkerAssignmentIsThrottledForTheSite", clusterName, databaseName,
                new Dictionary<string, string> {
                    { "antaresStamps", antaresStamps },
                    { "appName", appName },
                    { "startTime", startTime },
                    { "endTime", endTime },
                    { "siteNames", siteNames },
                    { "numRows", numRows },
                    { "privateStampName", privateStampName }
                });
        }

        [Description("Checks for DataRole upgrades by examining build versions. If there is more than 1 build version during the time period, that would indicate a new deployment.")]
        public Task<string> CheckDataRoleUpgrades(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Antares stamp name")] string antaresStamp,
            [Description("Bin interval (e.g., '10s')")] string binInterval,
            [Description("Start time in ISO format")] string startTime,
            [Description("End time in ISO format")] string endTime)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("AzureFunctions.FlexConsumption.CheckDataRoleUpgrades", clusterName, databaseName,
                new Dictionary<string, string> {
                    { "antaresStamp", antaresStamp },
                    { "binInterval", binInterval },
                    { "startTime", startTime },
                    { "endTime", endTime }
                });
        }

        [Description("Checks for ControllerRole upgrades by examining build versions. If there is more than 1 build version during the time period, that would indicate a new deployment.")]
        public Task<string> CheckControllerRoleUpgrades(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Antares stamp name")] string antaresStamp,
            [Description("Bin interval (e.g., '10s')")] string binInterval,
            [Description("Start time in ISO format")] string startTime,
            [Description("End time in ISO format")] string endTime)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("AzureFunctions.FlexConsumption.CheckControllerRoleUpgrades", clusterName, databaseName,
                new Dictionary<string, string> {
                    { "antaresStamp", antaresStamp },
                    { "binInterval", binInterval },
                    { "startTime", startTime },
                    { "endTime", endTime }
                });
        }

        [Description("Checks pool configuration version changes. If there is more than 1 version during the time period, that would indicate a new deployment.")]
        public Task<string> CheckPoolConfigVersion(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Antares stamp name")] string antaresStamp,
            [Description("Bin interval (e.g., '10s')")] string binInterval,
            [Description("Start time in ISO format")] string startTime,
            [Description("End time in ISO format")] string endTime)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("AzureFunctions.FlexConsumption.CheckPoolConfigVersion", clusterName, databaseName,
                new Dictionary<string, string> {
                    { "antaresStamp", antaresStamp },
                    { "binInterval", binInterval },
                    { "startTime", startTime },
                    { "endTime", endTime }
                });
        }

        [Description("Checks for FPS (Functions Pod Service) upgrades by examining version changes. If there is more than 1 build version during the time period, that would indicate a new deployment.")]
        public Task<string> CheckFpsUpgrades(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Kusto database name.")] string databaseName,
            [Description("Antares stamp name")] string antaresStamp,
            [Description("Bin interval (e.g., '10s')")] string binInterval,
            [Description("Start time in ISO format")] string startTime,
            [Description("End time in ISO format")] string endTime)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("AzureFunctions.FlexConsumption.CheckFpsUpgrades", clusterName, databaseName,
                new Dictionary<string, string> {
                    { "antaresStamp", antaresStamp },
                    { "binInterval", binInterval },
                    { "startTime", startTime },
                    { "endTime", endTime }
                });
        }
    }
}
