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
    public class BlobTriggerRCAPreflightPluginDefinition
    {
        private readonly IKustoPlugin _kustoPlugin;
        private const string DefaultClusterName = "wawscus";
        private const string DefaultDatabaseName = "wawsprod";
        private static readonly KustoDisplayOptions TableOnly = new() { ShowTable = true };


        public BlobTriggerRCAPreflightPluginDefinition(IKustoPlugin kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

        [Description(@"Checks worker/runtime health by counting FunctionCompleted events by hour.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> CheckFunctionRuntimeAndWorkers(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Start time yyyy-MM-ddTHH:mm:ss.fff")] string fromDate,
            [Description("End time yyyy-MM-ddTHH:mm:ss.fff")] string toDate,
            [Description("SiteName/application.")] string siteName,
            [Description("EventPrimaryStampName.")] string eventPrimaryStampName)
        {
            var a = await _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.BlobTriggerPreflight.CheckFunctionRuntimeAndWorkers", clusterName, DefaultDatabaseName,
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName}, {"eventPrimaryStampName", eventPrimaryStampName}
                }, TableOnly);
            return a;
        }

        [Description(@"Gets the most frequent EventIpAddress for the app/stamp.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> GetMostFrequentEventIpAddress(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Start time yyyy-MM-ddTHH:mm:ss.fff")] string fromDate,
            [Description("End time yyyy-MM-ddTHH:mm:ss.fff")] string toDate,
            [Description("SiteName/application.")] string siteName,
            [Description("EventPrimaryStampName.")] string eventPrimaryStampName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.BlobTriggerPreflight.GetMostFrequentEventIpAddress", clusterName, DefaultDatabaseName,
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName}, {"eventPrimaryStampName", eventPrimaryStampName}
                }, TableOnly);
        }

        [Description(@"Returns hourly FunctionCompleted counts for a function on a specific EventIpAddress.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> GetFunctionCompletedHourlyForIp(
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

            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.BlobTriggerPreflight.GetFunctionCompletedHourlyForIp", clusterName, DefaultDatabaseName,
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName},
                    {"eventPrimaryStampName", eventPrimaryStampName}, {"functionName", functionName}, {"eventIpAddress", eventIpAddress}
                }, TableOnly);
        }

        [Description(@"Finds a BlobDoesNotMatchPattern entry and extracts container/blob pattern.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> FindBlobMismatchPattern(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Start time yyyy-MM-ddTHH:mm:ss.fff")] string fromDate,
            [Description("End time yyyy-MM-ddTHH:mm:ss.fff")] string toDate,
            [Description("SiteName/application.")] string siteName,
            [Description("EventPrimaryStampName.")] string eventPrimaryStampName,
            [Description("Function name (e.g., Namespace.FunctionName).")] string functionName)
        {
            var message = FunctionsHelper.ProcessFunctionName(functionName, out bool ok);
            if (!ok) return Task.FromResult(message);

            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.BlobTriggerPreflight.FindBlobMismatchPattern", clusterName, DefaultDatabaseName,
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName},
                    {"eventPrimaryStampName", eventPrimaryStampName}, {"functionName", functionName}
                }, TableOnly);
        }

        [Description(@"Checks whether Storage blob listener started for a specific blob pattern.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> EnsureBlobStorageListenerStarted(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Start time yyyy-MM-ddTHH:mm:ss.fff")] string fromDate,
            [Description("End time yyyy-MM-ddTHH:mm:ss.fff")] string toDate,
            [Description("SiteName/application.")] string siteName,
            [Description("EventPrimaryStampName.")] string eventPrimaryStampName,
            [Description("Function name (e.g., Namespace.FunctionName).")] string functionName,
            [Description("Blob pattern (e.g., container/path/*.json).")] string blobPattern)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.BlobTriggerPreflight.EnsureBlobStorageListenerStarted", clusterName, DefaultDatabaseName,
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName},
                    {"eventPrimaryStampName", eventPrimaryStampName}, {"functionName", functionName}, {"blobPattern", blobPattern}
                }, TableOnly);
        }

        [Description(@"Confirms polling activity via PollBlobContainer events (hourly counts).")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> ConfirmPollingFunctioning(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Start time yyyy-MM-ddTHH:mm:ss.fff")] string fromDate,
            [Description("End time yyyy-MM-ddTHH:mm:ss.fff")] string toDate,
            [Description("SiteName/application.")] string siteName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.BlobTriggerPreflight.ConfirmPollingFunctioning", clusterName, DefaultDatabaseName,
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName}
                }, TableOnly);
        }

        [Description(@"Lists BlobDoesNotMatchPattern events for a function.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> CheckMismatchRulesForFunction(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Start time yyyy-MM-ddTHH:mm:ss.fff")] string fromDate,
            [Description("End time yyyy-MM-ddTHH:mm:ss.fff")] string toDate,
            [Description("SiteName/application.")] string siteName,
            [Description("EventPrimaryStampName.")] string eventPrimaryStampName,
            [Description("Function name (e.g., Namespace.FunctionName).")] string functionName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.BlobTriggerPreflight.CheckMismatchRulesForFunction", clusterName, DefaultDatabaseName,
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName},
                    {"eventPrimaryStampName", eventPrimaryStampName}, {"functionName", functionName}
                }, TableOnly);
        }

        [Description(@"Fetches BlobMessageEnqueued events and parses BlobName and QueueName.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> CheckBlobMessageEnqueuedForFunction(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Start time yyyy-MM-ddTHH:mm:ss.fff")] string fromDate,
            [Description("End time yyyy-MM-ddTHH:mm:ss.fff")] string toDate,
            [Description("SiteName/application.")] string siteName,
            [Description("EventPrimaryStampName.")] string eventPrimaryStampName,
            [Description("Function name (e.g., Namespace.FunctionName).")] string functionName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.BlobTriggerPreflight.CheckBlobMessageEnqueuedForFunction", clusterName, DefaultDatabaseName,
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName},
                    {"eventPrimaryStampName", eventPrimaryStampName}, {"functionName", functionName}
                }, TableOnly);
        }

        [Description(@"Verifies queue dequeue activity (GetMessages) for a QueueName.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> VerifyQueueDequeueGetMessages(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Start time yyyy-MM-ddTHH:mm:ss.fff")] string fromDate,
            [Description("End time yyyy-MM-ddTHH:mm:ss.fff")] string toDate,
            [Description("SiteName/application.")] string siteName,
            [Description("EventPrimaryStampName.")] string eventPrimaryStampName,
            [Description("Queue name to verify dequeues.")] string queueName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.BlobTriggerPreflight.VerifyQueueDequeueGetMessages", clusterName, DefaultDatabaseName,
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName},
                    {"eventPrimaryStampName", eventPrimaryStampName}, {"queueName", queueName}
                }, TableOnly);
        }

        [Description(@"Checks correlation across BlobMessageEnqueued, FunctionStarted, FunctionCompleted for a function.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> CheckFunctionTriggeredCorrelation(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Start time yyyy-MM-ddTHH:mm:ss.fff")] string fromDate,
            [Description("End time yyyy-MM-ddTHH:mm:ss.fff")] string toDate,
            [Description("SiteName/application.")] string siteName,
            [Description("EventPrimaryStampName.")] string eventPrimaryStampName,
            [Description("Function name (e.g., Namespace.FunctionName).")] string functionName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.BlobTriggerPreflight.CheckFunctionTriggeredCorrelation", clusterName, DefaultDatabaseName,
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName},
                    {"eventPrimaryStampName", eventPrimaryStampName}, {"functionName", functionName}
                }, TableOnly);
        }

        [Description(@"Fetches Site Metadata rows from AntaresReadOnlyViews for the site/stamp.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> FetchSiteMetadata(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Start time yyyy-MM-ddTHH:mm:ss.fff")] string fromDate,
            [Description("End time yyyy-MM-ddTHH:mm:ss.fff")] string toDate,
            [Description("SiteName/application.")] string siteName,
            [Description("EventPrimaryStampName.")] string eventPrimaryStampName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.BlobTriggerPreflight.FetchSiteMetadata", clusterName, DefaultDatabaseName,
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName},
                    {"eventPrimaryStampName", eventPrimaryStampName}
                }, TableOnly);
        }
    }
}
