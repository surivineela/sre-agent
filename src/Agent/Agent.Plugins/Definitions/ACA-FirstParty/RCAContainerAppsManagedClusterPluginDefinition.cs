using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Interfaces;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.KustoPlugin;
using Agent.Plugins.Kusto;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(IsFirstPartyOnly = true)]
    public class RCAContainerAppsManagedClusterPluginDefinition
    {
        private readonly IKustoPlugin _kustoPlugin;

        public RCAContainerAppsManagedClusterPluginDefinition(IKustoPlugin kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }


        [Description(@"
Purpose: Surface AKS managed-cluster operations in a time window to validate control-plane activity and correlate with ACA issues and pod restarts.

Scenario: Use after resolving the managed subscription/RG and CCP namespace via helper tools. Confirms if AKS operations align with outage windows or restart spikes.

Output (tab-separated):
- StartTime: Operation start.
- EndTime: Operation end.
- durationInMilliseconds: Elapsed time.
- operationName: High-level verb/action.
- suboperationName: Sub-step/detail if present.
- operationID: Server-side operation GUID.
- correlationID: Client/request correlation GUID.
- httpStatus: Final HTTP status code.
- subscriptionID: Managed cluster subscription.
- errorDetails: Error details if any.
- asyncErrorDetails: Async error details if any.
- resourceGroupName: Managed cluster RG.
- agentPoolName: Node pool if applicable.
- targetURI: ARM target of the call.
- resourceId: Full ARM resource id.
- Health: Unhealthy if error details exist
")]


        public async Task<string> GetAKSclusterMutatingOperations(
         [Description("Azure region.")] AzureRegion region,
         [Description("Start time of the query.")] DateTime fromDate,
         [Description("End time of the query.")] DateTime toDate,
         [Description("Name of the managed cluster. Used to resolve the environment context.")] string managedClusterName,
         
          [Description("managedSubscription: subscription of the managed cluster")] string managedSubscription)
        {
      
            return await _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("GetAKSclusterMutatingOperations", "akshuba.centralus", "AKSprod",
            new Dictionary<string, string> {
                 { "region", region.ToNormalizedString() },
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "resourceGroupName", $"{managedClusterName}-RG" },
            { "subscriptionId", managedSubscription },
            { "managedClusterName", managedClusterName },
            });
           
        }


        [Description(@"""
        Purpose:
        Retrieves a direct App Service Insights (ASI) page URL for a specific managed cluster associated with an Azure Container Apps environment.

        Scenario:
        Use this tool to get a diagnostic insights link for a managed cluster when the container app name is known.

        Output:
        Returns a string containing the ASI page URL:
        - ASIPageUrl: Direct URL to the ASI diagnostics page for the specified managed cluster
        """
        )]
        public async Task<string> GetASIPageForManagedClusterForApp(
         [Description("Azure region.")] AzureRegion region,
         [Description("Start time of the query.")] DateTime fromDate,
         [Description("End time of the query.")] DateTime toDate,
         [Description("Name of the container app. Used to resolve the environment context.")] string containerAppName,
         [Description("Name of the resource group hosting the ACA environment.")] string resourceGroupName,
         [Description("Azure subscription ID.")] string subscriptionId)
        {
            var clusterName = await _kustoPlugin.ExecuteFunctionInternalAsync("GetManagedClusterName", region,
                new Dictionary<string, string> {
            { "containerAppNameParam", containerAppName },
            { "resourceGroupParam", resourceGroupName },
            { "subscriptionParam", subscriptionId }
                });
            return await GetASIPageForManagedCluster(region, fromDate, toDate, clusterName.Result);
        }

        [Description(@"""
        Purpose:
        Retrieves a direct App Service Insights (ASI) page URL for a specific managed cluster associated with an Azure Container Apps environment.

        Scenario:
        Use this tool to get a diagnostic insights link for a managed cluster when the managed cluster name is already known.

        Output:
        Returns a string containing the ASI page URL:
        - ASIPageUrl: Direct URL to the ASI diagnostics page for the specified managed cluster
        """
        )]
        public Task<string> GetASIPageForManagedCluster(
        [Description("Azure region in lower case.")] AzureRegion region,
        [Description("Start time of the query.")] DateTime fromDate,
        [Description("End time of the query.")] DateTime toDate,
        [Description("Managed cluster name.")] string managedClusterName)
        {
            var basePath = "/services/ACA Azure Container Apps/pages/Managed Cluster";
            #pragma warning disable SYSLIB0013 
            var cleanPath = Uri.EscapeUriString(basePath); // DO NOT CHANGE TO EscapeDataString
            #pragma warning restore SYSLIB0013

            var query = $"managedClusterName={Uri.EscapeDataString(managedClusterName.Trim())}" +
                        $"&globalFrom={Uri.EscapeDataString(fromDate.ToString("M/d/yyyy hh:mm:ss tt"))}" +
                        $"&globalTo={Uri.EscapeDataString(toDate.ToString("M/d/yyyy hh:mm:ss tt"))}";

            var adxUri = $"https://asi.azure.ms{cleanPath}?{query}";

            return Task.FromResult($"ASI Page for managed cluster {adxUri}");
        }

        [Description(@"""
        Purpose:
        Retrieves the ccpNamespace of the aks cluster, required for other AKS queries.
       
        Scenario:
        Use this method when you need to obtain the ccpNamespace for an AKS cluster before performing other AKS cluster-specific queries that require this namespace identifier.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - clusterName: Name of the managed cluster
        - clusterVersion: Version of the cluster
        - resourceGroup: Resource group of the cluster
        - managedResourceGroup: Managed resource group
        - RPTenant: Resource provider tenant
        - clusterBirthdate: Cluster creation date
        - ccpNamespace: ccpNamespace of the aks cluster
        """
        )]
        public Task<string> GetAksClusterCcpNamespace(
        [Description("Azure region.")] AzureRegion region,
        [Description("Start time of the query.")] DateTime fromDate,
        [Description("End time of the query.")] DateTime toDate,
        [Description("aks cluster resource group")] string clusterResourceGroup,
        [Description("aks managed subscription ID.")] string managedSubscriptionId,
        [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetAksClusterCcpNamespace", region,
                new Dictionary<string, string> {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "resourceGroupName", clusterResourceGroup },
                    { "subscriptionId", managedSubscriptionId },
                    { "managedClusterName", managedClusterName }
                },
                groupName: "AKS");
        }

        [Description(@"""
Purpose: Retrieves AKS pod/container restarts within a given time window from AKS CCP logs. Useful for diagnosing crash loops or instability by showing the most recent terminated state for each container in a pod.

Scenario: Use when you have the ccpNamespace: ccpNamespace of the aks cluster and need to list restarted containers, their reasons, exit codes, images, and related details within a specific time range.

Output: CSV (tab-separated) with columns:
- PreciseTimeStamp: Time termination was recorded (or finishedAt).
- container: Container name.
- reason: Termination reason (e.g., OOMKilled, Error).
- exitCode: Process exit code.
- image: Container image.
- containerID: Runtime container ID.
- pod: Pod name.
- ns: Kubernetes namespace.
- restartCount: Restart count.
- startedAt: Last start time.
- finishedAt: Last stop time.
- message: Termination message.
- state: JSON of container state.
- username: Initiating user principal.
- userAgent: Client user agent string.
""")]
        public Task<string> GetAKSPodRestarts(
    [Description("ccpNamespace of the aks cluster")] string ccpNamespace,
    [Description("Start time (UTC) for the query window.")] DateTime fromDate,
    [Description("End time (UTC) for the query window.")] DateTime toDate)
        {
            var parameters = new Dictionary<string, string>
    {
        { "fromDate", fromDate.ToUniversalTime().ToString("o") },
        { "toDate", toDate.ToUniversalTime().ToString("o") },
        { "ccpNamespace", ccpNamespace }
    };

            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync(
                "GetAKSPodRestarts",
                "akshuba.centralus",
                "AKSccplogs",
                parameters
            );
        }


        [Description(@"""
        Purpose:
        Retrieves system component error events for the given managed cluster.

        Scenario:
        Use this tool to when need to check system components error.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - PodName: Name of the pod
        - ContainerName: Name of the container
        - RestartCount: Number of restarts
        - LastStateReason: Reason for the last state
        - LastStateExitCode: Exit code for the last state
        - StateWaitingMessage: Waiting state message
        - StateWaitingReason: Waiting state reason
        """
        )]
        public Task<string> GetSystemComponentErrorEvents(
            [Description("Azure region.")] AzureRegion region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetSystemComponentErrorEvents", region,
                new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName }
                });
        }

        [Description(@"""
        Purpose:
        Retrieves system component CPU usage for the given managed cluster.

        Scenario:
        Use this tool to analyze CPU utilization across system components in a managed cluster. 
        Check whether high CPU usage is contributing to performance issues within the cluster.
        
        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - TimestampUtc: Timestamp of the metric
        - podName: Name of the pod
        - cores: CPU cores used
        - limit: CPU limit for the pod
        - pct: Percentage of CPU used relative to the limit
        """
        )]
        public Task<string> GetSystemComponentCpuUsage(
            [Description("Azure region.")] AzureRegion region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetSystemComponentCpuUsage", region,
                new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "region", region.ToNormalizedString() }
                });
        }

        [Description(@"""
        Purpose:
        Retrieves system component memory usage for the given managed cluster.

        Scenario:
        Use this tool to analyze memory utilization across system components in a managed cluster. 
        Check whether high memory usage is contributing to performance issues within the cluster.
        
        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - TimestampUtc: Timestamp of the metric
        - podName: Name of the pod
        - usageMb: Memory usage in MB
        - limit: Memory limit for the pod in MB
        - pct: Percentage of memory used relative to the limit
        """
        )]
        public Task<string> GetSystemComponentMemoryUsage(
            [Description("Azure region.")] AzureRegion region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetSystemComponentMemoryUsage", region,
                new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "region", region.ToNormalizedString() }
                });
        }
    }
}
