// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Plugins.Helpers;
using Agent.Plugins.Interface;
using Agent.Plugins.Kusto;
using Microsoft.Graph.Drives.Item.Items.Item.Workbook.Functions.VarA;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(IsFirstPartyOnly = true, Category = ToolCategories.LogQuery, ResourceType = ToolResourceTypes.AppService)]
    public class EventHubTriggerRCAPreflightPluginDefinition
    {
        private readonly IKustoPlugin _kustoPlugin;
        private const string DefaultClusterName = "wawscus";
        private const string DefaultDatabaseName = "wawsprod";
        private static readonly KustoDisplayOptions TableOnly = new() { ShowTable = true };


        public EventHubTriggerRCAPreflightPluginDefinition(IKustoPlugin kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

        //This function gets the Kusto cluster name for a given site name by querying Issue time analytics events, instead of querying recent time (GetKustoClusterFromSiteName). It is due to
        //customer might redeploy the app to another stamp as a workaround, so recent time analytics events might not have the correct stamp name.
        [Description(@"""
        Retrieves the Kusto cluster name based on the site name by looking up Issue time analytics events.
        Use this tool to determine which Kusto cluster should be used for queries based on the site name.
        Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
        - EventPrimaryStampName: Event primary stamp name associated with the site.
        - KustoCluster: Name of the Kusto cluster associated with the site name.
            """
        )]
        [AgentTool(ToolMode.Auto)]
        public Task<string> GetKustoClusterFromSiteNameAtIssueTime(            
            [Description("Start time yyyy-MM-ddTHH:mm:ss.fff")] string fromDate,
            [Description("End time yyyy-MM-ddTHH:mm:ss.fff")] string toDate,
            [Description("SiteName/application.")] string siteName
            )
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("GetKustoClusterFromSiteNameAtIssueTime", DefaultClusterName, DefaultDatabaseName,
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName}
                }, TableOnly);            
        }



        [Description(@"Gets EventHub Trigger related extension version for the app.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> GetEventHubTriggerExtensionVersion(
        [Description("Kusto cluster name.")] string clusterName,
        [Description("Start time yyyy-MM-ddTHH:mm:ss.fff")] string fromDate,
        [Description("End time yyyy-MM-ddTHH:mm:ss.fff")] string toDate,
        [Description("SiteName/application.")] string siteName,
        [Description("EventPrimaryStampName.")] string eventPrimaryStampName)
        {
            var message = FunctionsHelper.ProcessEventPrimaryStampName(eventPrimaryStampName, out bool isValid);
            if (!isValid)
            {
                return Task.FromResult(message);
            }

            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.EventHubTriggerPreflight.GetEventHubTriggerExtensionVersion", clusterName, DefaultDatabaseName,
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName}, {"eventPrimaryStampName", eventPrimaryStampName}
                }, TableOnly);
        }
                     

        [Description(@"Checks whether Event Hub listener started for a specific entity path pattern.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> EnsureEventHubListenerStarted(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Start time yyyy-MM-ddTHH:mm:ss.fff")] string fromDate,
            [Description("End time yyyy-MM-ddTHH:mm:ss.fff")] string toDate,
            [Description("SiteName/application.")] string siteName,
            [Description("EventPrimaryStampName.")] string eventPrimaryStampName,
            [Description("Function name (e.g., Namespace.FunctionName).")] string functionName,
            [Description("EventHub Name, ConsumerGroup pattern (e.g., eventHub='foo-in-topic', consumerGroup='$Default').")] string entityPathPattern)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.EventHubTriggerPreflight.EnsureEventHubListenerStarted", clusterName, DefaultDatabaseName,
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName},
                    {"eventPrimaryStampName", eventPrimaryStampName}, {"functionName", functionName}, {"entityPathPattern", entityPathPattern}
                }, TableOnly);
        }


        [Description(@"Fetches EventHubMessageEnqueued events and PartitionId, Offset, EnqueueTimeUtc, SequenceNumber and MessageCount.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> CheckEventHubMessageEnqueuedForFunction(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Start time yyyy-MM-ddTHH:mm:ss.fff")] string fromDate,
            [Description("End time yyyy-MM-ddTHH:mm:ss.fff")] string toDate,
            [Description("SiteName/application.")] string siteName,
            [Description("EventPrimaryStampName.")] string eventPrimaryStampName,
            [Description("Function name (e.g., Namespace.FunctionName).")] string functionName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.EventHubTriggerPreflight.CheckEventHubMessageEnqueuedForFunction", clusterName, DefaultDatabaseName,
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName},
                    {"eventPrimaryStampName", eventPrimaryStampName}, {"functionName", functionName}
                }, TableOnly);
        }


        [Description(@"Checks correlation across EventHubMessageEnqueued, FunctionStarted, FunctionCompleted for a function.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> CheckEventHubFunctionTriggeredCorrelation(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Start time yyyy-MM-ddTHH:mm:ss.fff")] string fromDate,
            [Description("End time yyyy-MM-ddTHH:mm:ss.fff")] string toDate,
            [Description("SiteName/application.")] string siteName,
            [Description("EventPrimaryStampName.")] string eventPrimaryStampName,
            [Description("Function name (e.g., Namespace.FunctionName).")] string functionName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.EventHubTriggerPreflight.CheckEventHubFunctionTriggeredCorrelation", clusterName, DefaultDatabaseName,
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName},
                    {"eventPrimaryStampName", eventPrimaryStampName}, {"functionName", functionName}
                }, TableOnly);
        }

        [Description(@"Summarizes EventHub message processing delays (in seconds) at multiple percentiles (5, 50, 90, 95, 99) to analyze latency distribution.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> CheckEventHubMessageDelayPercentiles(
         [Description("Kusto cluster name.")] string clusterName,
         [Description("Start time yyyy-MM-ddTHH:mm:ss.fff")] string fromDate,
         [Description("End time yyyy-MM-ddTHH:mm:ss.fff")] string toDate,
         [Description("SiteName/application.")] string siteName,
         [Description("EventPrimaryStampName.")] string eventPrimaryStampName,
         [Description("Function name (e.g., Namespace.FunctionName).")] string functionName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.EventHubTriggerPreflight.CheckEventHubMessageDelayPercentiles", clusterName, DefaultDatabaseName,
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName},
                    {"eventPrimaryStampName", eventPrimaryStampName}, {"functionName", functionName}
                }, TableOnly);
        }

        [Description(@"Get EventHub Partition Count.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> CheckEventHubPartitionCount(
         [Description("Kusto cluster name.")] string clusterName,
         [Description("Start time yyyy-MM-ddTHH:mm:ss.fff")] string fromDate,
         [Description("End time yyyy-MM-ddTHH:mm:ss.fff")] string toDate,
         [Description("SiteName/application.")] string siteName,
         [Description("EventPrimaryStampName.")] string eventPrimaryStampName,
         [Description("Function name (e.g., Namespace.FunctionName).")] string functionName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.EventHubTriggerPreflight.CheckEventHubPartitionCount", clusterName, DefaultDatabaseName,
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName},
                    {"eventPrimaryStampName", eventPrimaryStampName}, {"functionName", functionName}
                }, TableOnly);
        }

        [Description(@"Retrieves the maximum number of EventHub messages processed concurrently within a given time window to identify peak throughput capacity")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> CheckEventHubMessagePeakThroughput(
           [Description("Kusto cluster name.")] string clusterName,
            [Description("Start time yyyy-MM-ddTHH:mm:ss.fff")] string fromDate,
            [Description("End time yyyy-MM-ddTHH:mm:ss.fff")] string toDate,
            [Description("SiteName/application.")] string siteName,
            [Description("EventPrimaryStampName.")] string eventPrimaryStampName,
            [Description("Function name (e.g., Namespace.FunctionName).")] string functionName,
            [Description("EventIpAddress to filter by.")] string eventIpAddress)
        {
            var message = FunctionsHelper.ProcessFunctionName(functionName, out bool ok);
            if (!ok) return Task.FromResult(message);

            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.EventHubTriggerPreflight.CheckEventHubMessagePeakThroughput", clusterName, DefaultDatabaseName,
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName},
                    {"eventPrimaryStampName", eventPrimaryStampName}, {"functionName", functionName}, {"eventIpAddress", eventIpAddress}
                }, TableOnly);
        }

    }
}
