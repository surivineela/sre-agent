// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Data.DatabaseClients.GraphDbClient;
using FirstPartyAgent.Constants;
using FirstPartyAgent.Core.Plugins.Interfaces;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Plugins.Definitions
{
    // These are tools exposed to any-sub agent that uses this plugin but mostly it will be used by 'SessionsAgent'
    // Note!!: If this plugin is used by other agent, then we are mixing the concerns and we need to refactor this plugin
    public class ContainerAppSessionsPluginDefinition(IContainerAppSessionsPlugin Plugin)
    {
        private readonly IContainerAppSessionsPlugin _plugin = Plugin;


        [KernelFunction(KernelFunctionNames.ACA.GetSessionPoolInfo)]
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
            return await _plugin.GetSessionPoolInfo(region, fromDate, toDate, subscriptionId, resourceGroupName, sessionPoolName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetChangesInSessionPool)]
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
            return await _plugin.GetChangesInSessionPool(region, fromDate, toDate, subscriptionId, resourceGroupName, sessionPoolName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetSessionPodLogs)]
        [Description(
                    @"Get errors in session pod logs in the given time range for a given pod name.")]
        public async Task<string> GetSessionPodLogs(
            [Description("region")] string region,
            [Description("fromDate")] DateTime fromDate,
            [Description("toDate")] DateTime toDate,
            [Description("podName")] string podName)
        {
            return await _plugin.GetSessionPodLogs(region, fromDate, toDate, podName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetSessionPoolCreateOrUpdateLogs)]
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
            return await _plugin.GetSessionPoolCreateOrUpdateLogs(region, fromDate, toDate, subscriptionId, resourceGroupName, sessionPoolName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetCodeInterpreterSessionExecutionEventLogs)]
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
            return await _plugin.GetCodeInterpreterSessionExecutionEventLogs(region, fromDate, toDate, subscriptionId, resourceGroupName, sessionPoolName, sessionIdentifier);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetCustomContainerSessionActivatorLogs)]
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
            return await _plugin.GetCustomContainerSessionActivatorLogs(region, fromDate, toDate, subscriptionId, resourceGroupName, sessionPoolName, managedEnvironmentName);
        }
    }
}
