// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.KustoPlugin;
using Agent.Plugins.Services.Interfaces;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin]
    public class RCAContainerAppsSessionsPluginDefinition
    {
        private readonly IKustoPluginChat _kustoPlugin;

        public RCAContainerAppsSessionsPluginDefinition(IKustoPluginChat kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

        [Description(
                    @"Get session pool information for a given session pool name and time range.")]
        public async Task<string> GetSessionPoolInfo(
            [Description("region")] string region,
            [Description("fromDate")] DateTime fromDate,
            [Description("toDate")] DateTime toDate,
            [Description("subscriptionId")] string subscriptionId,
            [Description("resourceGroupName")] string resourceGroupName,
            [Description("sessionPoolName")] string sessionPoolName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetSessionPoolInfo", region,
             new Dictionary<string, string> {
                 { "fromDate", fromDate.ToString() },
                 { "toDate", toDate.ToString() },
                 { "subscriptionId", subscriptionId },
                 { "resourceGroupName", resourceGroupName },
                 { "sessionPoolName", sessionPoolName }
             }
             );
        }

        [Description(
                    @"Get changes in session pool for a given subscription, resource group, and session pool name.")]
        public async Task<string> GetChangesInSessionPool(
            [Description("region")] string region,
            [Description("fromDate")] DateTime fromDate,
            [Description("toDate")] DateTime toDate,
            [Description("subscriptionId")] string subscriptionId,
            [Description("resourceGroupName")] string resourceGroupName,
            [Description("sessionPoolName")] string sessionPoolName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetChangesInSessionPool", region,
             new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "subscriptionId", subscriptionId },
                        { "resourceGroupName", resourceGroupName },
                        { "sessionPoolName", sessionPoolName }
             });
        }

        [Description(
                    @"Get errors in session pod logs in the given time range for a given pod name.")]
        public async Task<string> GetSessionPodLogs(
            [Description("region")] string region,
            [Description("fromDate")] DateTime fromDate,
            [Description("toDate")] DateTime toDate,
            [Description("podName")] string podName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetSessionPodLogs", region,
             new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "podName", podName }
             });
        }

        [Description(
                    @"Get errors in session pool create or update logs for a given subscription, resource group, and session pool name.")]
        public async Task<string> GetSessionPoolCreateOrUpdateLogs(
            [Description("region")] string region,
            [Description("fromDate")] DateTime fromDate,
            [Description("toDate")] DateTime toDate,
            [Description("subscriptionId")] string subscriptionId,
            [Description("resourceGroupName")] string resourceGroupName,
            [Description("sessionPoolName")] string sessionPoolName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetSessionPoolCreateOrUpdateLogs", region,
             new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "subscriptionId", subscriptionId },
                        { "resourceGroupName", resourceGroupName },
                        { "sessionPoolName", sessionPoolName }
             });
        }

        [Description(
                    @"Get errors in code interpreter session execution event logs for a given subscription, resource group, and session pool name.
                      To fetch logs for a specific session, provide the session identifier.
                      If empty, it will fetch logs for a random session execution in the given time range.
")]
        public async Task<string> GetCodeInterpreterSessionExecutionEventLogs(
            [Description("region")] string region,
            [Description("fromDate")] DateTime fromDate,
            [Description("toDate")] DateTime toDate,
            [Description("subscriptionId")] string subscriptionId,
            [Description("resourceGroupName")] string resourceGroupName,
            [Description("sessionPoolName")] string sessionPoolName,
            [Description("sessionIdentifier")] string sessionIdentifier = "")
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetCodeInterpreterSessionExecutionEventLogs", region,
             new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "subscriptionId", subscriptionId },
                        { "resourceGroupName", resourceGroupName },
                        { "sessionPoolName", sessionPoolName },
                        { "sessionIdentifier", sessionIdentifier }
             });
        }

        [Description(
                    @"Get errors in custom container session activator logs for a given subscription, resource group, managedEnvironment and session pool name.
                      Use this to fetch errors in pod allocation logs for a new session request. 
")]
        public async Task<string> GetCustomContainerSessionActivatorLogs(
            [Description("region")] string region,
            [Description("fromDate")] DateTime fromDate,
            [Description("toDate")] DateTime toDate,
            [Description("subscriptionId")] string subscriptionId,
            [Description("resourceGroupName")] string resourceGroupName,
            [Description("sessionPoolName")] string sessionPoolName,
            [Description("managedEnvironmentName")] string managedEnvironmentName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetCustomContainerSessionActivatorLogs", region,
             new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "subscriptionId", subscriptionId },
                        { "resourceGroupName", resourceGroupName },
                        { "sessionPoolName", sessionPoolName },
                        { "managedEnvironmentName", managedEnvironmentName }
             });
        }
    }
}
