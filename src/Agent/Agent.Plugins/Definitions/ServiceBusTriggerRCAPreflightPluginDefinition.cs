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
    public class ServiceBusTriggerRCAPreflightPluginDefinition
    {
        private readonly IKustoPlugin _kustoPlugin;
        private const string DefaultClusterName = "wawscus";
        private const string DefaultDatabaseName = "wawsprod";
        private static readonly KustoDisplayOptions TableOnly = new() { ShowTable = true };


        public ServiceBusTriggerRCAPreflightPluginDefinition(IKustoPlugin kustoPlugin)
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



        [Description(@"Gets ServiceBus Trigger related extension version for the app.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> GetServiceBusTriggerExtensionVersion(
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

            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.ServiceBusTriggerPreflight.GetServiceBusTriggerExtensionVersion", clusterName, DefaultDatabaseName,
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName}, {"eventPrimaryStampName", eventPrimaryStampName}
                }, TableOnly);
        }
                     

        [Description(@"Checks whether Service Bus listener started for a specific entity path pattern.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> EnsureServiceBusListenerStarted(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Start time yyyy-MM-ddTHH:mm:ss.fff")] string fromDate,
            [Description("End time yyyy-MM-ddTHH:mm:ss.fff")] string toDate,
            [Description("SiteName/application.")] string siteName,
            [Description("EventPrimaryStampName.")] string eventPrimaryStampName,
            [Description("Function name (e.g., Namespace.FunctionName).")] string functionName,
            [Description("ServiceBus entity path pattern (e.g., entityPath='topic1/Subscriptions/sub').")] string entityPathPattern)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.ServiceBusTriggerPreflight.EnsureServiceBusListenerStarted", clusterName, DefaultDatabaseName,                                                                      
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName},
                    {"eventPrimaryStampName", eventPrimaryStampName}, {"functionName", functionName}, {"entityPathPattern", entityPathPattern}
                }, TableOnly);
        }

      
        
        [Description(@"Fetches ServiceBusMessageEnqueued events and parses Entity Path and QueueName.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> CheckServiceBusMessageEnqueuedForFunction(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Start time yyyy-MM-ddTHH:mm:ss.fff")] string fromDate,
            [Description("End time yyyy-MM-ddTHH:mm:ss.fff")] string toDate,
            [Description("SiteName/application.")] string siteName,
            [Description("EventPrimaryStampName.")] string eventPrimaryStampName,
            [Description("Function name (e.g., Namespace.FunctionName).")] string functionName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.ServiceBusTriggerPreflight.CheckServiceBusMessageEnqueuedForFunction", clusterName, DefaultDatabaseName,
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName},
                    {"eventPrimaryStampName", eventPrimaryStampName}, {"functionName", functionName}
                }, TableOnly);
        }
        
          
        
        [Description(@"Checks correlation across ServiceBusMessageEnqueued, FunctionStarted, FunctionCompleted for a function.")]
        [AgentTool(ToolMode.Auto)]
        public Task<string> CheckServiceBusFunctionTriggeredCorrelation(
            [Description("Kusto cluster name.")] string clusterName,
            [Description("Start time yyyy-MM-ddTHH:mm:ss.fff")] string fromDate,
            [Description("End time yyyy-MM-ddTHH:mm:ss.fff")] string toDate,
            [Description("SiteName/application.")] string siteName,
            [Description("EventPrimaryStampName.")] string eventPrimaryStampName,
            [Description("Function name (e.g., Namespace.FunctionName).")] string functionName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("RCA.ServiceBusTriggerPreflight.CheckServiceBusFunctionTriggeredCorrelation", clusterName, DefaultDatabaseName,
                new Dictionary<string, string> {
                    {"startTime", fromDate}, {"endTime", toDate}, {"siteName", siteName},
                    {"eventPrimaryStampName", eventPrimaryStampName}, {"functionName", functionName}
                }, TableOnly);
        }
        
    }
}
