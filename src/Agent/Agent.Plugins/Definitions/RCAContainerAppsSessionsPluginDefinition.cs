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
    [AgentToolPlugin(IsFirstPartyOnly = true)]
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
                    @"Check if allocation availability for the legion pool has dropped. 
                      It returns all instances where allocation rate was less than 100% for the given legion pod pool name in the specified time range.
")]
        public async Task<string> GetCodeInterpreterSessionLegionPoolAvailability(
            [Description("region")] string region,
            [Description("fromDate")] DateTime fromDate,
            [Description("toDate")] DateTime toDate,
            [Description("legionPodPoolName")] string legionPodPoolName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetCodeInterpreterSessionLegionPoolAvailability", region,
             new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "legionPodPoolName", legionPodPoolName },
                        { "legionEnvironmentName", region }
             },
             "legion");
        }


        [Description(
                    @"Get a specific allocated pod for a code interpreter session in the given time range.
                      If sessionIdentifier is provided, it will fetch the pod details for that specific session. Otherwise it will fetch a random pod allocated for a session in the given time range.
                      It returns the session identifier, podName and poolType of the session.
")]
        public async Task<string> GetCodeInterpreterSessionAllocatedPods(
            [Description("region")] string region,
            [Description("fromDate")] DateTime fromDate,
            [Description("toDate")] DateTime toDate,
            [Description("subscriptionId")] string subscriptionId,
            [Description("resourceGroupName")] string resourceGroupName,
            [Description("sessionPoolName")] string sessionPoolName,
            [Description("sessionIdentifier")] string sessionIdentifier = "")
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetCodeInterpreterSessionAllocatedPods", region,
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
                    @"Get errors events for a code interpreter session pod with a specific podName/podId.
")]
        public async Task<string> GetCodeInterpreterSessionPodEventLogs(
            [Description("region")] string region,
            [Description("fromDate")] DateTime fromDate,
            [Description("toDate")] DateTime toDate,
            [Description("podName")] string podName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetCodeInterpreterSessionPodEventLogs", region,
             new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "podName", podName }
             },
             "legion");
        }

        [Description(
                    @"Get error logs for a code interpreter session pod with a specific podName/podId.
                      Use this to fetch error logs for a code interpreter session pod in the given time range.
                      Note that this only returns error logs, not all logs.
")]                     
        public async Task<string> GetCodeInterpreterSessionPodLogs(
            [Description("region")] string region,
            [Description("fromDate")] DateTime fromDate,
            [Description("toDate")] DateTime toDate,
            [Description("podName")] string podName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetCodeInterpreterSessionPodLogs", region,
             new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "podName", podName }
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

        [Description(
                    @"Get all failed envoy requests for a custom container session in the given time range.
                      This is useful to identify the issues with failed requests.
                      For each failed request, it returns the `Status` which is the status code and `ResponseCodeDetails` which is the envoy response code for the request.
")]
        public async Task<string> GetCustomContainerSessionEnvoyRequests(
            [Description("region")] string region,
            [Description("fromDate")] DateTime fromDate,
            [Description("toDate")] DateTime toDate,
            [Description("sessionPoolName")] string sessionPoolName,
            [Description("managedClusterName")] string managedClusterName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetCustomContainerSessionEnvoyRequests", region,
             new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "sessionPoolName", sessionPoolName },
                        { "managedClusterName", managedClusterName },
                        { "region", region }
             });
        }

        [Description(
                    @"Get the status of a custom container session legion pool for a given subscription, resource group, and session pool name.
                      It returns the number of pods in pool which are ready, pending , allocated and inactive.
")]
        public async Task<string> GetCustomContainerSessionLegionPoolStatus(
          [Description("region")] string region,
          [Description("fromDate")] DateTime fromDate,
          [Description("toDate")] DateTime toDate,
          [Description("subscriptionId")] string subscriptionId,
          [Description("resourceGroupName")] string resourceGroupName,
          [Description("sessionPoolName")] string sessionPoolName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetCustomContainerSessionLegionPoolStatus", region,
             new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "subscriptionId", subscriptionId },
                        { "resourceGroupName", resourceGroupName },
                        { "sessionPoolName", sessionPoolName },
                        { "legionEnvironmentName", region }
             },
             "legion");
        }

    }
}
