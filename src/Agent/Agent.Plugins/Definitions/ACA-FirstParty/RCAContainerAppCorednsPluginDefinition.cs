// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions
{

    [AgentToolPlugin(IsFirstPartyOnly = true)]
    public class RCAContainerAppCorednsPluginDefinition
    {
        private readonly IKustoPlugin _kustoPlugin;

        public RCAContainerAppCorednsPluginDefinition(IKustoPlugin kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }
                [Description("""
Purpose:
Retrieves the list of custom DNS servers set for a container app environment at the start and end of a time window.

Scenario:
Use this tool to check if custom DNS servers are configured or to compare DNS settings over time.

Output:
Returns tab-separated table data in CSV format. Column headers:
- DNSServers: Custom DNS servers found in the environment
- DNSStatus: Status message indicating if custom DNS servers are configured
- StartTime: The earliest time custom DNS servers were found in the time window
- EndTime: The latest time custom DNS servers were found in the time window
"""
)]
        public Task<string> GetCustomDNSServers(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetCustomDNSServersOverTime", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName }
                });
        }

                [Description("""
Purpose:
Checks the health status of upstream custom DNS servers for a managed cluster, grouped by node or VMSS, within a time range.

Scenario:
Use this tool to find out if any upstream DNS server had health check failures.

Output:
Returns tab-separated table data in CSV format. Column headers:
- PreciseTimeStamp: Time of the metric
- totalCount: Total count for the coredns_forward_healthcheck_failures_total metric in the time window
- VMNodeWhereMetricCaptured: Name of the node or VMSS
- PodName: Name of the pod (if applicable)
"""
)]
        public Task<string> GetUpstreamCustomDNSServerHealthStatus(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetGenericMetricCountData", region.NormalizeLocation(),
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName },
                    { "metricName", "coredns_forward_healthcheck_failures_total" },
                    { "duration", GetDuration(fromDate, toDate) },
                    { "threshold", "0" }
                });
        }

                [Description("""
Purpose:
Retrieves the average DNS resolution latency (in seconds) handled by CoreDNS for a managed cluster, grouped by node or VMSS, over a time range.

Scenario:
Use this tool to monitor DNS performance and detect latency spikes.

Output:
Returns tab-separated table data in CSV format. Column headers:
- PreciseTimeStamp: Time of the metric
- avgValue: Average value for the coredns_dns_request_duration_seconds metric in the time window
- totalCount: Number of samples used for the average
- VMNodeWhereMetricCaptured: Name of the node or VMSS
- PodName: Name of the pod (if applicable)
"""
)]
        public Task<string> GetAverageLatencyOfDNSResolutionRequests(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetGenericMetricAverageValueData", region.NormalizeLocation(),
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName },
                    { "metricName", "coredns_dns_request_duration_seconds" },
                    { "duration", GetDuration(fromDate, toDate) },
                    { "threshold", "0" }
                });
        }

                [Description("""
Purpose:
Retrieves the average forwarding latency (in seconds) for DNS requests sent by CoreDNS to upstream servers in a managed cluster, grouped by node or VMSS, over a time range.

Scenario:
Use this tool to check for delays in DNS forwarding to upstream servers.

Output:
Returns tab-separated table data in CSV format. Column headers:
- PreciseTimeStamp: Time of the metric
- avgValue: Average value for the coredns_forward_request_duration_seconds metric in the time window
- totalCount: Number of samples used for the average
- VMNodeWhereMetricCaptured: Name of the node or VMSS
- PodName: Name of the pod (if applicable)
"""
)]
        public Task<string> GetAverageLatencyOfUpstreamDNSResolutionForwardRequests(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetGenericMetricAverageValueData", region.NormalizeLocation(),
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName },
                    { "metricName", "coredns_forward_request_duration_seconds" },
                    { "duration", GetDuration(fromDate, toDate) },
                    { "threshold", "0" }
                });
        }

                [Description("""
Purpose:
Retrieves the number of CoreDNS process panics in a managed cluster, grouped by node or VMSS, over a time range.

Scenario:
Use this tool to detect CoreDNS crashes or restarts due to panics.

Output:
Returns tab-separated table data in CSV format. Column headers:
- PreciseTimeStamp: Time of the metric
- totalCount: Total count for the coredns_panics_total metric in the time window
- VMNodeWhereMetricCaptured: Name of the node or VMSS
- PodName: Name of the pod (if applicable)
"""
)]
        public Task<string> GetCoreDNSProcessCrashesCount(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetGenericMetricCountData", region.NormalizeLocation(),
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName },
                    { "metricName", "coredns_panics_total" },
                    { "duration", GetDuration(fromDate, toDate) },
                    { "threshold", "0" }
                });
        }

                [Description("""
Purpose:
Retrieves the number of failed CoreDNS configuration reloads in a managed cluster, grouped by node or VMSS, over a time range.

Scenario:
Use this tool to find configuration reload issues that may affect DNS.

Output:
Returns tab-separated table data in CSV format. Column headers:
- PreciseTimeStamp: Time of the metric
- totalCount: Total count for the coredns_reload_failed_total metric in the time window
- VMNodeWhereMetricCaptured: Name of the node or VMSS
- PodName: Name of the pod (if applicable)
"""
)]
        public Task<string> GetCoreDNSConfigReloadFailuresCount(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetGenericMetricCountData", region.NormalizeLocation(),
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName },
                    { "metricName", "coredns_reload_failed_total" },
                    { "duration", GetDuration(fromDate, toDate) },
                    { "threshold", "0" }
                });
        }

                [Description("""
Purpose:
Retrieves the total number of DNS requests handled by CoreDNS in a managed cluster, grouped by node or VMSS, over a time range.

Scenario:
Use this tool to monitor DNS traffic volume and trends.

Output:
Returns tab-separated table data in CSV format. Column headers:
- PreciseTimeStamp: Time of the metric
- totalCount: Total count for the coredns_dns_requests_total metric in the time window
- VMNodeWhereMetricCaptured: Name of the node or VMSS
- PodName: Name of the pod (if applicable)
"""
)]
        public Task<string> GetCoreDNSTotalDNSRequestCount(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetGenericMetricCountData", region.NormalizeLocation(),
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName },
                    { "metricName", "coredns_dns_requests_total" },
                    { "duration", GetDuration(fromDate, toDate) },
                    { "threshold", "0" }
                });
        }

                [Description("""
Purpose:
Retrieves the number of DNS queries rejected by CoreDNS due to reaching the maximum allowed concurrent upstream requests in a managed cluster, grouped by node or VMSS, over a time range.

Scenario:
Use this tool to identify DNS performance issues caused by concurrency limits.

Output:
Returns tab-separated table data in CSV format. Column headers:
- PreciseTimeStamp: Time of the metric
- totalCount: Total count for the coredns_forward_max_concurrent_rejects_total metric in the time window
- VMNodeWhereMetricCaptured: Name of the node or VMSS
- PodName: Name of the pod (if applicable)
"""
)]
        public Task<string> GetCoreDNSForwardConcurrentRejectsCount(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetGenericMetricCountData", region.NormalizeLocation(),
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName },
                    { "metricName", "coredns_forward_max_concurrent_rejects_total" },
                    { "duration", GetDuration(fromDate, toDate) },
                    { "threshold", "0" }
                });
        }

                [Description("""
Purpose:
Retrieves the average time (in seconds) CoreDNS takes to program DNS records from Kubernetes service and endpoint objects in a managed cluster, grouped by node or VMSS, over a time range.

Scenario:
Use this tool to detect delays in DNS record updates from Kubernetes.

Output:
Returns tab-separated table data in CSV format. Column headers:
- PreciseTimeStamp: Time of the metric
- avgValue: Average value for the coredns_kubernetes_dns_programming_duration_seconds metric in the time window
- totalCount: Number of samples used for the average
- VMNodeWhereMetricCaptured: Name of the node or VMSS
- PodName: Name of the pod (if applicable)
"""
)]
        public Task<string> GetAverageLatencyOfCoreDNSKubernetesDNSProgramming(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetGenericMetricAverageValueData", region.NormalizeLocation(),
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName },
                    { "metricName", "coredns_kubernetes_dns_programming_duration_seconds" },
                    { "duration", GetDuration(fromDate, toDate) },
                    { "threshold", "0" }
                });
        }

                [Description("""
Purpose:
Retrieves CoreDNS pod failure events for a container app environment, grouped by node or VMSS, over a time range.

Scenario:
Use this tool to find CoreDNS pod failures in the environment.

Output:
Returns tab-separated table data in CSV format. Column headers:
- Pod: Name of the pod
- Node: Name of the node or VMSS
- msg: Failure message
- Reason: Reason for the failure
- FailureStartTime: Time of the first failure in the window
- FailureEndTime: Time of the last failure in the window
- FailureCount: Number of failures in the window
"""
)]
        public Task<string> GetCorednsPodFailureEvents(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodFailureEvents", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName },
                    { "threshold", "5" },
                    { "podNamePrefix", "coredns" },
                    { "podNamespace", "kube-system" }
                });
        }

                [Description("""
Purpose:
Retrieves swift bootstrap agent pod failure events for a container app environment, grouped by node or VMSS, over a time range.

Scenario:
Use this tool to find swift bootstrap agent pod failures in the environment.

Output:
Returns tab-separated table data in CSV format. Column headers:
- Pod: Name of the pod
- Node: Name of the node or VMSS
- msg: Failure message
- Reason: Reason for the failure
- FailureStartTime: Time of the first failure in the window
- FailureEndTime: Time of the last failure in the window
- FailureCount: Number of failures in the window
"""
)]
        public Task<string> GetSwiftBootstrapAgentPodFailureEvents(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodFailureEvents", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName },
                    { "threshold", "5" },
                    { "podNamePrefix", "swift-bootstrap-agent" },
                    { "podNamespace", "kube-system" }
                });
        }

                [Description("""
Purpose:
Retrieves swift bootstrap agent pod health status for a container app environment, grouped by node or VMSS, over a time range.

Scenario:
Use this tool to check the health of swift bootstrap agent pods in the environment.

Output:
Returns tab-separated table data in CSV format. Column headers:
- StartTime: Time of the first health status record
- EndTime: Time of the last health status record
- PodName: Name of the pod
- NodeName: Name of the node or VMSS
- PodStatus: Status of the pod
- Health: Health state (Healthy/Degraded)
- restartCount: Number of restarts
- ContainerName: Name of the container (if applicable)
- ContainerState: State of the container (if applicable)
- ContainerImage: Image of the container (if applicable)
"""
)]
        public Task<string> GetSwiftBootstrapAgentPodHealthStatus(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodHealthStatus", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName },
                    { "podNamePrefix", "swift-bootstrap-agent" },
                    { "podNamespace", "k8se-system" }
                });
        }

                [Description("""
Purpose:
Retrieves DNS config update status for a container app environment over a time range.

Scenario:
Use this tool to check if DNS config updates were successful or failed.

Output:
Returns tab-separated table data in CSV format. Column headers:
- StartTime: Time when the status period started
- EndTime: Time when the status period ended
- DNSConfigStatus: Status of the DNS config (Healthy/Degraded)
- SessionIndex: Index of the status session
- VMNode: Name of the node or VMSS
"""
)]
        public Task<string> GetDNSConfigUpdateStatus(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetDNSConfigUpdateStatus", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName }
                });
        }

                [Description("""
Purpose:
Checks if the custom DNS server failed to resolve the root domain (.) for a container app environment over a time range.

Scenario:
Use this tool to detect DNS resolution failures for the root domain.

Output:
Returns tab-separated table data in CSV format. Column headers:
- FailureCount: Number of failures detected
- StartTime: Time of the first failure
- EndTime: Time of the last failure
"""
)]
        public Task<string> CheckIfDNSServerFailedToResolveDot(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("CheckIfDNSServerFailedToResolveDot", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName },
                    { "threshold", "0" }
                });
        }
        private static string GetDuration(DateTime fromDate, DateTime toDate)
        {
            var totalHours = (toDate - fromDate).TotalHours;
            var totalDays = (toDate - fromDate).TotalDays;
            // Use the lowest frequency possible for the given range
            if (totalDays > 5)
            {
                return "1d";
            }
            if (totalHours > 24)
            {
                return "1h";
            }
            return "1m";
        }
    }
}
