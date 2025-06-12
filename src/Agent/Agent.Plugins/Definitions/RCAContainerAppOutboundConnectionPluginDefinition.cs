// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.Kusto;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin]
    public class RCAContainerAppOutboundConnectionPluginDefinition
    {
        private readonly IKustoPluginChat _kustoPlugin;

        public RCAContainerAppOutboundConnectionPluginDefinition(IKustoPluginChat kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

        [Description("""
            Analyze the distribution of connection states to identify patterns in connection termination for a specific container app pod.
            This query examines TCP connection sequences to categorize connections by their termination state.

            What this metric measures:
            - TCP Handshake Failures: Connections that failed to establish properly
            - Gracefully closed: Connections terminated with proper FIN handshake
            - Reset connections: Connections terminated abruptly with RST packets
            - Half-close scenarios: One-way connection terminations
            - Idle timeouts: Connections that timed out without proper closure

            When it is applicable:
            Useful for identifying connection quality issues, network problems, or application-level connection handling issues.
        """)]
        public Task<string> GracefulConnectionCount(
            DateTime fromDate,
            DateTime toDate,
            string podGuid,
            string region)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GracefulConnectionCount", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "legionEnvironmentName", region },
                    { "podGuid", podGuid }
                }, groupName: "Legion");
        }

        [Description("""
            Retrieve details of connections that were not gracefully closed to identify problematic outbound connections for a specific container app pod.
            This query filters out gracefully terminated connections and provides detailed information about problematic connections.

            What this metric measures:
            - Non-gracefully terminated connections with timing information
            - Destination details including resolved domain names
            - Connection duration and termination reasons
            - Packet sequences showing connection behavior

            When it is applicable:
            Useful for identifying specific endpoints or connection patterns that are causing issues, network connectivity problems, or application bugs.
        """)]
        public Task<string> GetTerminatedConnectionsForPod(
            DateTime fromDate,
            DateTime toDate,
            string podGuid,
            string region)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetTerminatedConnectionsForPod", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "legionEnvironmentName", region },
                    { "podGuid", podGuid }
                }, groupName: "Legion");
        }

        [Description("""
            Retrieve DNS server manager operations to identify any DNS resolution issues that might affect outbound connections for a specific container app pod.
            This query examines logs from DNS-related components to identify configuration issues or operational problems.

            What this metric measures:
            - DNS server manager operations and their outcomes
            - DNS listener manager activities
            - CoreDNS manager operations
            - Timing and trace information for DNS operations

            When it is applicable:
            Useful for correlating connection issues with DNS problems, identifying DNS configuration changes, or troubleshooting name resolution failures.
        """)]
        public Task<string> DnsServerManagerOperation(
            DateTime fromDate,
            DateTime toDate,
            string managedCluster,
            string podName,
            string region)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("DnsServerManagerOperation", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "legionEnvironmentName", region },
                    { "resourceNamespace", managedCluster },
                    { "podName", podName }
                }, groupName: "Legion");
        }

        [Description("""
        Retrieve a direct ASI (App Service Insights) page URL for a specific Pod in a Legion cluster.
        This link provides diagnostic insights into the specified Pod.

        Inputs:
        - podName: Name of the Pod.
        - managedCluster: Namespace of the resource (e.g., ccpNamespace).
        - fromDate / toDate: Time range for diagnostic analysis.
        """)]
        public Task<string> GetASIPageForLegionPod(
            [Description("Name of the Pod.")] string podName,
            [Description("Namespace of the resource (e.g., ccpNamespace).")] string managedCluster,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate)
        {
            var basePath = "/services/Legion/pages/Pod";
            var cleanPath = Uri.EscapeUriString(basePath); // DO NOT CHANGE TO EscapeDataString

            var query = $"PodName={Uri.EscapeDataString(podName.Trim())}" +
                        $"&ResourceNamespace={Uri.EscapeDataString(managedCluster.Trim())}" +
                        $"&globalFrom={Uri.EscapeDataString(fromDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))}" +
                        $"&globalTo={Uri.EscapeDataString(toDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))}";

            var asiUri = $"https://asi.azure.ms{cleanPath}?{query}";

            return Task.FromResult($"ASI Page for Legion Pod {asiUri}");
        }

        [Description("""
            Retrieve PodGuid and related information for a specific container app pod using its name and namespace.
            This query searches system logs to find the PodGuid which is required for subsequent connection analysis.

            What this provides:
            - PodGuid: Required identifier for connection queries
            - LegionEnvironmentName: Environment information
            - CenturionRoleId/NestedRoleId: Role identifiers
            - Geneva trace URL: Direct link to trace logs
            - KustoCluster: Cluster information for queries

            When it is applicable:
            Essential first step when you have pod name and namespace but need the PodGuid for connection analysis.
        """)]
        public Task<string> GetPodGuidFromName(
            DateTime fromDate,
            DateTime toDate,
            string podName,
            string resourceNamespace,
            string region)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodGuidFromName", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "podName", podName },
                    { "resourceNamespace", resourceNamespace }
                }, groupName: "Legion");
        }
    }
}
