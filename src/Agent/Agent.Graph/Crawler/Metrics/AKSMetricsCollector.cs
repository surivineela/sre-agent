// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Globalization;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Logging;
using Agent.Prometheus;
using Agent.Prometheus.Services;
using Gremlin.Net.Process.Traversal;
using k8s;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace Agent.Graph.Crawler.Metrics;
public class AKSMetricsCollector : IResourceMetricsCollector
{
    private readonly ILogger<AKSMetricsCollector> _logger;
    private readonly IAzureMetricsClient _azureMetricsClient;
    private readonly IPrometheusQueryService _prometheusQueryService;
    private readonly IKubernetesClientFactory _kubernetesClientFactory;
    private readonly string _prometheusQueryEndpoint;
    public string ResourceType { get; set; } = Constants.AzureKubernetesServiceDeploymentType;

    public AKSMetricsCollector(
        ILogger<AKSMetricsCollector> logger,
        IAzureMetricsClient azureMetricsClient,
        IKubernetesClientFactory kubernetesClientFactory,
        IPrometheusQueryService prometheusQueryService,
        DashboardSettings dashboardSettings)
    {
        _logger = logger;
        _kubernetesClientFactory = kubernetesClientFactory;
        _azureMetricsClient = azureMetricsClient;
        _prometheusQueryService = prometheusQueryService;
        _prometheusQueryEndpoint = dashboardSettings.PrometheusUrl;
    }

    public async Task<AppHealthInfo> CollectMetricsAsync(GraphNode gnode)
    {
        if (gnode is not KubernetesNamespacedResourceNode node)
        {
            _logger.LogInternalWarning($"Node {gnode.GetNodeId()} is not an KubernetesResourceNode");
            return new AppHealthInfo { };
        }

        var resourceId = node.GetNodeId();
        if (resourceId == null)
        {
            _logger.LogInternalWarning($"Resource id for node {node.GetNodeLabel()} cannot be null or empty");
            return new AppHealthInfo { };
        }

        var now = DateTime.UtcNow;
        var startTime = now.AddMinutes(-30);

        try
        {
            var avgCpuUsage = await GetAvgCpuUsageAsync(node);
            var avgMemUsage = await GetAvgMemoryUsageAsync(node);
            var availability = await GetAvailabilityAsync(node);
            var cost = await _azureMetricsClient.GetCostAsync(resourceId, now);

            var appHealthInfo = new AppHealthInfo
            {
                AvgMemoryUsage = Math.Round(avgMemUsage, 2),
                AvgCpuUsage = Math.Round(avgCpuUsage, 2),
                Availability = Math.Round(availability, 2),
                Transactions = 0, // TODO(jianbosun): add requests count to support it
                Costs = Math.Round(cost, 2),
                Health = availability >= 99.0 ? ScorecardHealthState.Healthy :
                        availability >= 95.0 ? ScorecardHealthState.Degraded :
                        ScorecardHealthState.Unhealthy,
            };

            return appHealthInfo;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Failed to get metrics for the App Service {node.GetNodeId()}");
        }

        return new AppHealthInfo { };
    }

    private async Task<double> GetAvgCpuUsageAsync(KubernetesNamespacedResourceNode node)
    {
        string _namespace = node.Namespace;
        string workloadType = node.Kind.TrimEnd('s').ToLowerInvariant();
        string workloadName = node.ResourceName;
        _logger.LogInternalInformation($"Getting average CPU usage for AKS {workloadType}: {_namespace}/{workloadName}");
        return await GetAzureMonitorPrometheusMetricsAsync(_namespace, workloadType, workloadName, "cpu");
    }

    private async Task<double> GetAvgMemoryUsageAsync(KubernetesNamespacedResourceNode node)
    {
        string _namespace = node.Namespace;
        string workloadType = node.Kind.TrimEnd('s').ToLowerInvariant();
        string workloadName = node.ResourceName;
        _logger.LogInternalInformation($"Getting average Memory usage for AKS {workloadType}: {_namespace}/{workloadName}");
        return await GetAzureMonitorPrometheusMetricsAsync(_namespace, workloadType, workloadName, "memory");
    }

    private async Task<double> GetAvailabilityAsync(KubernetesNamespacedResourceNode node)
    {
        string _namespace = node.Namespace;
        string workloadType = node.Kind.TrimEnd('s').ToLowerInvariant();
        string workloadName = node.ResourceName;
        var aksResourceId = node.ClusterResourceId;
        _logger.LogInternalInformation($"Getting availability for AKS {workloadType}: {_namespace}/{workloadName}");
        try
        { 
        
            switch (workloadType)
            {
                case "deployment":
                    var client = await _kubernetesClientFactory.CreateKubernetesClientFromResourceIdForCrawlerAsync(aksResourceId);
                    var status = await client.AppsV1.ReadNamespacedDeploymentStatusAsync(workloadName, _namespace);
                    return (double)status.Status.AvailableReplicas / (double)status.Status.Replicas * 100;
                case "statefulset":
                    var client2 = await _kubernetesClientFactory.CreateKubernetesClientFromResourceIdForCrawlerAsync(aksResourceId);
                    var status2 = await client2.AppsV1.ReadNamespacedStatefulSetStatusAsync(workloadName, _namespace);
                    return (double)status2.Status.AvailableReplicas / (double)status2.Status.Replicas * 100;
                case "pod":
                    var client3 = await _kubernetesClientFactory.CreateKubernetesClientFromResourceIdForCrawlerAsync(aksResourceId);
                    var status3 = await client3.CoreV1.ReadNamespacedPodStatusAsync(workloadName, _namespace);
                    return (double)status3.Status.ContainerStatuses.Count(s => s.Ready) / (double)status3.Status.ContainerStatuses.Count * 100;
                default:
                    _logger.LogInternalWarning($"Unsupported availability type for AKS {workloadType}: {_namespace}/{workloadName}");
                    return 100;
            }
        }
        catch (Exception ex)
        {
            // Check if it's a 404 Not Found error (resource doesn't exist)
            if (ex is k8s.Autorest.HttpOperationException httpEx && 
                httpEx.Response?.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Resource not found (404), just return without logging
                return 100;
            }
            _logger.LogInternalWarning(ex, $"Failed to get availability for AKS {workloadType}: {_namespace}/{workloadName}, default to 100.");
            return 100;
        }
    }

    private async Task<double> GetAzureMonitorPrometheusMetricsAsync(
               string _namespace,
               string workloadType,
               string workloadName,
               string metricType)
    {
        if (string.IsNullOrEmpty(_prometheusQueryEndpoint))
        {
            return 0;
        }

        try
        {

            // Build the PromQL query based on the specified metric type
            string query = BuildPromQuery(metricType, _namespace, workloadType, workloadName);

            if (string.IsNullOrEmpty(query) || query.StartsWith("No query", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogInternalWarning(
                    "Failed to build a valid PromQL query for metric type '{MetricType}' in namespace '{Namespace}', workload type '{WorkloadType}', and workload name '{WorkloadName}'.",
                    metricType, _namespace, workloadType, workloadName);
                return 0;
            }

            _logger?.LogDebug(
                "Executing PromQL against Azure Monitor Prometheus endpoint '{Endpoint}': {Query}",
                _prometheusQueryEndpoint, query);

            // Query the Prometheus endpoint using the injected service
            var response = await _prometheusQueryService.QueryInstantAsync(_prometheusQueryEndpoint, query);

            switch (response)
            {
                case SuccessVectorResponse successVector:
                    var vectorData = successVector.Data;
                    if (vectorData?.Result == null || !vectorData.Result.Any())
                    {
                        return 0;
                    }
                    double sum = 0;
                    double count = 0;
                    foreach (var resultItem in vectorData.Result)
                    {
                        // --- Metric Value ---
                        double timestamp = resultItem.Value.Item1; // Unix timestamp (seconds)
                        string rawValue = resultItem.Value.Item2;
                        DateTimeOffset dateTime = DateTimeOffset.FromUnixTimeSeconds((long)timestamp);

                        if (double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out double numericValue))
                        {
                            // Check for NaN or Infinity which Prometheus can return
                            if (double.IsNaN(numericValue) || double.IsInfinity(numericValue))
                            {
                                continue;
                            }
                            sum += numericValue;
                            count += 1;
                        }
                    }
                    return sum / count;
                case ErrorResponse errorResponse:
                    _logger?.LogInternalError(
                        "Received an error response from Azure Monitor Prometheus for metric type '{MetricType}' in namespace '{Namespace}', workload type '{WorkloadType}', and workload name '{WorkloadName}': {Error}",
                        metricType, _namespace, workloadType, workloadName, errorResponse.Error);
                    return 0;

                default:
                    _logger?.LogInternalWarning(
                        "Received an unexpected response type '{ResponseType}' from Azure Monitor Prometheus for metric type '{MetricType}' in namespace '{Namespace}', workload type '{WorkloadType}', and workload name '{WorkloadName}'.",
                        response.GetType().Name, metricType, _namespace, workloadType, workloadName);
                    return 0;
            }

        }
        catch (HttpRequestException httpEx)
        {
            _logger?.LogInternalError(
                httpEx,
                "HTTP error while querying Azure Monitor Prometheus for metric type '{MetricType}' in namespace '{Namespace}', workload type '{WorkloadType}', and workload name '{WorkloadName}'.",
                metricType, _namespace, workloadType, workloadName);
            return 0;
        }
        catch (Exception ex)
        {
            _logger?.LogInternalError(
                ex,
                "Unexpected error while fetching Prometheus metrics for metric type '{MetricType}' in namespace '{Namespace}', workload type '{WorkloadType}', and workload name '{WorkloadName}'.",
                metricType, _namespace, workloadType, workloadName);
            return 0;
        }
    }

    // Requires Azure Monitor for Prometheus addon to be enabled on AKS.
    private string BuildPromQuery(string metricType, string _namespace, string workloadType, string workloadName)
    {
        string filter = "";
        switch (workloadType)
        {
            case "deployment":
            case "statefulset":
                filter = $"pod=~\"{workloadName}\""; // Update filter for deployment and statefulset
                break;
            case "pod":
                filter = $"pod=\"{workloadName}\"";
                break;
            default:
                _logger?.LogInternalWarning(
                    "Unsupported workload type '{WorkloadType}' for AKS in namespace '{Namespace}' and workload name '{WorkloadName}'.",
                    workloadType, _namespace, workloadName);
                return $"No query configured for metric type '{metricType}' in namespace '{_namespace}', workload type '{workloadType}', and workload name '{workloadName}'.";
        }

        switch (metricType.ToLowerInvariant())
        {
            case "memory":
                return $@"
                            100 *  (
                                sum by (pod) (
                                    container_memory_working_set_bytes{{{filter},namespace=""{_namespace}"",container!=""""}}
                                )
                                / on (pod)
                                min by (pod) (
                                    (
                                        kube_node_status_allocatable{{resource=""memory""}} * on (node) group_right kube_pod_info{{{filter},namespace=""{_namespace}""}}
                                    )   
                                    or
                                    (
                                        kube_pod_container_resource_limits{{{filter},namespace=""{_namespace}"", resource=""memory""}}
                                    )
                                ) 
                            )
                        ";
            case "cpu":
                return $@"
                            100 *  (
                                sum by (pod) (
                                    rate(container_cpu_usage_seconds_total{{{filter},namespace=""{_namespace}"",container!=""""}}[2m])
                                )
                                / on (pod)
                                min by (pod) (
                                    (
                                        kube_node_status_allocatable{{resource=""cpu""}} * on (node) group_right kube_pod_info{{{filter},namespace=""{_namespace}""}}
                                    )   
                                    or
                                    (
                                        kube_pod_container_resource_limits{{{filter},namespace=""{_namespace}"", resource=""cpu""}}
                                    )
                                ) 
                            )
                        ";

            // Default case for custom queries or other unhandled metric types
            default:
                _logger?.LogInternalWarning(
                    "No query configured for metric type '{MetricType}' in namespace '{Namespace}', workload type '{WorkloadType}', and workload name '{WorkloadName}'.",
                    metricType, _namespace, workloadType, workloadName);

                return $"No query configured for metric type '{metricType}' in namespace '{_namespace}', workload type '{workloadType}', and workload name '{workloadName}'.";

        }
    }
}
