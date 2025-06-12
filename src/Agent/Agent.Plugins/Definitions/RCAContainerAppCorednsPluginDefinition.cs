// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions
{

    [AgentToolPlugin]
    public class RCAContainerAppCorednsPluginDefinition
    {
        private readonly IKustoPluginChat _kustoPlugin;

        public RCAContainerAppCorednsPluginDefinition(IKustoPluginChat kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

        [Description(
            @"Get list of custom DNS servers configured for the container app environment at start and end of time window. It also checks if custom DNS servers are configured or not.
            If no data is returned then ask to validate inputs again as it should never be the case.")]
        public Task<string> GetCustomDNSServers(
            string region,
            DateTime fromDate,
            DateTime toDate,
            string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetCustomDNSServersOverTime", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName }
                });
        }

        [Description(@"
                Retrieve the health status of upstream custom DNS servers for a given managed Kubernetes cluster, segmented by node or VMSS, within a specified time range.
                If the query returns results, it indicates that the corresponding upstream DNS server experienced health check failures (i.e., it is unhealthy).

                What this metric measures:  If no results are returned, the upstream DNS server is considered healthy during that time frame.
                When it is applicable: CoreDNS could not reach upstream DNS servers 
            ")]
        public Task<string> GetUpstreamCustomDNSServerHealthStatus(
            string region,
            DateTime fromDate,
            DateTime toDate,
            string managedClusterName)
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

        [Description(@"
                Retrieve the average latency (in seconds) of DNS resolution requests handled by CoreDNS within a given managed Kubernetes cluster, segmented by node or VMSS over a specified time range.
                The query calculates the average time (in seconds) CoreDNS takes to resolve DNS queries by dividing the total duration of all DNS requests by the total number of requests.
                This metric is useful for identifying performance degradation or latency spikes in DNS resolution.

                What this metric measures: Measures total time CoreDNS takes to serve any DNS request, regardless of whether it uses cache, forwards it, or uses plugins. End-to-end latency from the client's perspective.
                When it is applicable: Helps detect increased DNS resolution latency, which may impact application performance or indicate upstream DNS server slowness.
            ")]
        public Task<string> GetAverageLatencyOfDNSResolutionRequests(
            string region,
            DateTime fromDate,
            DateTime toDate,
            string managedClusterName)
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

        [Description(@"
                Retrieve the average forwarding latency (in seconds) of DNS resolution requests handled by CoreDNS within a managed Kubernetes cluster, segmented by node or VMSS over a specified time range.
                The query calculates the average time (in seconds) CoreDNS takes to forward DNS queries to upstream servers and receive responses.

                What this metric measures: Measures only the forwarding time, how long CoreDNS takes to send a DNS request to an upstream DNS server and receive a response.
                When it is applicable: Helps detect increased DNS resolution latency, which may indicate upstream DNS server slowness or network issues.
            ")]
        public Task<string> GetAverageLatencyOfUpstreamDNSResolutionForwardRequests(
            string region,
            DateTime fromDate,
            DateTime toDate,
            string managedClusterName)
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

        [Description(@"
        Retrieve the number of panic events triggered by the CoreDNS process within a managed Kubernetes cluster, segmented by node or VMSS over a specified time range.
        The query counts how many times CoreDNS encountered a runtime panic, which may result in process crashes or restarts.

        What this metric measures:
        Tracks the total number of CoreDNS panics caused by unexpected failures such as plugin bugs or misconfigurations.

        When it is applicable:
        Useful for identifying critical issues affecting CoreDNS stability that may lead to DNS resolution failures or service interruptions.
                ")]
        public Task<string> GetCoreDNSProcessCrashesCount(
            string region,
            DateTime fromDate,
            DateTime toDate,
            string managedClusterName)
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

        [Description(@"
        Retrieve the number of failed CoreDNS configuration reload attempts within a managed Kubernetes cluster, segmented by node or VMSS over a specified time range.
        The query counts how often CoreDNS failed to apply a new configuration, which can impact DNS functionality.

        What this metric measures:
        Tracks the total number of times CoreDNS attempted but failed to reload its configuration.

        When it is applicable:
        Useful for detecting configuration issues or malformed updates that may prevent CoreDNS from functioning correctly after changes.
                ")]
        public Task<string> GetCoreDNSConfigReloadFailuresCount(
            string region,
            DateTime fromDate,
            DateTime toDate,
            string managedClusterName)
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

        [Description(@"
        Retrieve the total number of DNS requests handled by CoreDNS within a managed Kubernetes cluster, segmented by node or VMSS over a specified time range.
        This query helps assess the DNS query load and usage trends across the cluster.

        What this metric measures:
        Tracks the cumulative number of DNS requests received by CoreDNS.

        When it is applicable:
        Useful for understanding DNS traffic volume, detecting sudden spikes or drops in request rates, and capacity planning.
        ")]
        public Task<string> GetCoreDNSTotalDNSRequestCount(
            string region,
            DateTime fromDate,
            DateTime toDate,
            string managedClusterName)
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

        [Description(@"
        Retrieve the number of DNS queries rejected by CoreDNS due to exceeding the maximum allowed concurrent upstream requests, within a managed Kubernetes cluster. 
        Results are segmented by node or VMSS over a specified time range.

        What this metric measures:
        Counts the total number of DNS queries dropped when CoreDNS reached its limit for simultaneous upstream connections.

        When it is applicable:
        Useful for identifying DNS performance bottlenecks caused by concurrency limits, which may lead to dropped queries or degraded resolution performance.
        ")]
        public Task<string> GetCoreDNSForwardConcurrentRejectsCount(
            string region,
            DateTime fromDate,
            DateTime toDate,
            string managedClusterName)
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

        [Description(@"
        Retrieve the average time (in seconds) taken by CoreDNS to program DNS records from Kubernetes service and endpoint objects, within a managed Kubernetes cluster. 
        Results are segmented by node or VMSS over a specified time range.

        What this metric measures:
        Measures the duration CoreDNS takes to process Kubernetes object updates and make DNS records available.

        When it is applicable:
        Helps detect delays in DNS record propagation caused by slow synchronization between Kubernetes API and CoreDNS, which can lead to temporary name resolution failures.
        ")]
        public Task<string> GetAverageLatencyOfCoreDNSKubernetesDNSProgramming(
            string region,
            DateTime fromDate,
            DateTime toDate,
            string managedClusterName)
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

        [Description(
            @"Get coredns pod failure events for the container app environment per node/vmss for a given time frame")]
        public Task<string> GetCorednsPodFailureEvents(
            string region,
            DateTime fromDate,
            DateTime toDate,
            string managedClusterName)
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

        [Description(
            @"Get swift bootstrap agent pod failure events for the container app environment per node/vmss for a given time frame")]
        public Task<string> GetSwiftBootstrapAgentPodFailureEvents(
            string region,
            DateTime fromDate,
            DateTime toDate,
            string managedClusterName)
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

        [Description(
            @"Get swift bootstrap agent pod health status for the container app environment per node/vmss for a given time frame")]
        public Task<string> GetSwiftBootstrapAgentPodHealthStatus(
            string region,
            DateTime fromDate,
            DateTime toDate,
            string managedClusterName)
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

        [Description(
            @"Get DNS config update status for the container app environment for a given time frame")]
        public Task<string> GetDNSConfigUpdateStatus(
            string region,
            DateTime fromDate,
            DateTime toDate,
            string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetDNSConfigUpdateStatus", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName }
                });
        }

        [Description(
            @"Check if the Custom DNS server failed to resolve the dot (.) for the container app environment for a given time frame")]
        public Task<string> CheckIfDNSServerFailedToResolveDot(
            string region,
            DateTime fromDate,
            DateTime toDate,
            string managedClusterName)
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
