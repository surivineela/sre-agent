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
    [AgentToolPlugin(IsFirstPartyOnly = true)]
    public class RCAContainerAppOutboundConnectionPluginDefinition
    {
        private readonly IKustoPlugin _kustoPlugin;

        public RCAContainerAppOutboundConnectionPluginDefinition(IKustoPlugin kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

        [Description(@"""
        Purpose:
        Analyzes the distribution of TCP connection states for a specific container app pod.

        Scenario:
        Use this tool to identify patterns in connection termination and detect connection quality issues.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - connectionState: The type of connection termination (e.g., Gracefully closed, TCP Handshake Failure, Reset, Half-close, Idle timeout)
        - Count: Number of connections in each state
        """
        )]
        public Task<string> GracefulConnectionCount(
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Unique identifier (GUID) of the pod.")] string podGuid,
            [Description("Azure region name.")] AzureRegion region)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GracefulConnectionCount", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "legionEnvironmentName", region.ToNormalizedString() },
                    { "podGuid", podGuid }
                }, groupName: "Legion");
        }

        [Description(@"""
        Purpose:
        Retrieves details of outbound connections that were not gracefully closed for a specific container app pod.

        Scenario:
        Use this tool to identify problematic connections and analyze connection failures.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - StartTime: Connection start time (UTC)
        - EndTime: Connection end time (UTC)
        - Protocol: Network protocol used
        - Direction: Connection direction (Inbound/Outbound)
        - RouteKind: Route type for the connection
        - SourceIpAddress: Source IP address
        - DestinationIpAddress: Destination IP address (masked)
        - DestinationDomain: Resolved domain name for the destination, if available
        - SourcePort: Source port number
        - DestinationPort: Destination port number
        - PacketSequence: Sequence of packets observed
        - Duration
        - RemovalReason
        """
        )]
        public Task<string> GetTerminatedConnectionsForPod(
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Unique identifier (GUID) of the pod.")] string podGuid,
            [Description("Azure region name.")] AzureRegion region)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetTerminatedConnectionsForPod", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "legionEnvironmentName", region.ToNormalizedString() },
                    { "podGuid", podGuid }
                }, groupName: "Legion");
        }

        [Description(@"""
        Purpose:
        Retrieves DNS server manager operations and related logs for a specific container app pod.

        Scenario:
        Use this tool to identify DNS resolution issues that may affect outbound connections.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - TIMESTAMP: Date and time of the log entry
        - Message: Log message content
        - OperationName: Name of the DNS operation
        - Value: Value or result of the operation
        - TraceID: Trace identifier for the log entry
        """
        )]
        public Task<string> DnsServerManagerOperation(
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Namespace of the managed cluster.")] string managedCluster,
            [Description("Name of the pod.")] string podName,
            [Description("Azure region name.")] AzureRegion region)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("DnsServerManagerOperation", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "legionEnvironmentName", region.ToNormalizedString() },
                    { "resourceNamespace", managedCluster },
                    { "podName", podName }
                }, groupName: "Legion");
        }

        [Description(@"""
        Purpose:
        Retrieves a direct App Service Insights (ASI) page URL for diagnostics of a specific pod in a Legion cluster.

        Scenario:
        Use this tool to get a diagnostic insights link for a pod over a specified time range.

        Output:
        Returns a string containing the ASI page URL:
        - ASIPageUrl: Direct URL to the ASI diagnostics page for the specified pod
        """
        )]
        public Task<string> GetASIPageForLegionPod(
            [Description("Name of the pod.")] string podName,
            [Description("Namespace of the managed cluster.")] string managedCluster,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate)
        {
            #pragma warning disable SYSLIB0013
            var basePath = "/services/Legion/pages/Pod";
            var cleanPath = Uri.EscapeUriString(basePath); // DO NOT CHANGE TO EscapeDataString

            var query = $"PodName={Uri.EscapeDataString(podName.Trim())}" +
                        $"&ResourceNamespace={Uri.EscapeDataString(managedCluster.Trim())}" +
                        $"&globalFrom={Uri.EscapeUriString(fromDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))}" +
                        $"&globalTo={Uri.EscapeUriString(toDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))}";

            var asiUri = $"https://asi.azure.ms{cleanPath}?{query}";

            return Task.FromResult($"ASI Page for Legion Pod {asiUri}");
            #pragma warning restore SYSLIB0013
        }

        [Description(@"""
        Purpose:
        Retrieves PodGuid and related information for a specific container app pod using its name and namespace.

        Scenario:
        Use this tool to find the PodGuid and environment details required for connection analysis.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - LegionEnvironmentName: Name of the Legion environment
        - PodName: Name of the pod
        - ResourceNamespace: Namespace of the resource
        - PodGuid: Unique identifier (GUID) of the pod
        - CenturionRoleId: Centurion role identifier
        - NestedRoleId: Nested role identifier
        - geneva_url: Direct link to Geneva trace logs
        - env_dt_traceId: Trace identifier for the log entry
        """
        )]
        public Task<string> GetPodGuidFromName(
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the pod.")] string podName,
            [Description("Namespace of the resource.")] string resourceNamespace,
            [Description("Azure region name.")] AzureRegion region)
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
